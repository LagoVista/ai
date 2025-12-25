using LagoVista.AI.Helpers;
using LagoVista.AI.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// Reads a streaming /responses SSE stream and returns the completed response JSON
    /// (the inner "response" object).
    ///
    /// The reader is a small state machine:
    /// - buffer data lines
    /// - flush on boundaries (blank line, new event, [DONE], EOF)
    /// - capture deltas as they arrive
    /// - capture the completed payload once
    /// </summary>
    public sealed class OpenAIStreamingResponseReader : IOpenAIStreamingResponseReader
    {
        private readonly IAdminLogger _logger;
        private readonly ILLMEventPublisher _events;
        private readonly IAgentStreamingContext _streamingUi;

        public OpenAIStreamingResponseReader(IAdminLogger logger, ILLMEventPublisher events, IAgentStreamingContext streamingUi)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _streamingUi = streamingUi ?? throw new ArgumentNullException(nameof(streamingUi));
        }

        public async Task<InvokeResult<string>> ReadAsync(
            HttpResponseMessage httpResponse,
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            // Streaming reader assumes a successful HTTP response;
            // higher layers are responsible for status handling.
            if (httpResponse == null)
            {
                return InvokeResult<string>.FromError("HttpResponseMessage is null.", "OPENAI_STREAM_NULL_RESPONSE");
            }

            // Session id is required so deltas can be correlated and published.
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return InvokeResult<string>.FromError("sessionId is required for streaming reader.", "OPENAI_STREAM_MISSING_SESSION");
            }

            try
            {
                using (var stream = await httpResponse.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    // Accumulates state for the *current* SSE event.
                    var acc = new SseAccumulator();

                    // Read the SSE stream line-by-line.
                    while (!reader.EndOfStream)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return InvokeResult<string>.Abort();
                        }

                        var line = await reader.ReadLineAsync();
                        if (line == null)
                        {
                            break;
                        }

                        // Blank line = end of current SSE event.
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            await FlushAsync(acc, sessionId, cancellationToken);
                            continue;
                        }

                        // New event header = boundary for previous buffered data.
                        if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
                        {
                            // Some servers omit the blank line; treat this as a hard boundary.
                            await FlushAsync(acc, sessionId, cancellationToken);

                            acc.CurrentEvent = line.Substring("event:".Length).Trim();
                            continue;
                        }

                        // Data lines carry JSON payload fragments.
                        if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            var dataPart = line.Substring("data:".Length).Trim();

                            // [DONE] explicitly terminates the stream.
                            if (string.Equals(dataPart, "[DONE]", StringComparison.Ordinal))
                            {
                                await FlushAsync(acc, sessionId, cancellationToken);
                                break;
                            }

                            acc.Data.AppendLine(dataPart);
                            continue;
                        }

                        // All other SSE lines (comments, extensions) are ignored.
                    }

                    // If the stream ends without a trailing blank line,
                    // flush any buffered event one last time.
                    await FlushAsync(acc, sessionId, cancellationToken);

                    // A valid streaming response must contain a completed event.
                    if (string.IsNullOrWhiteSpace(acc.CompletedEventJson))
                    {
                        _logger.AddError(
                            "[OpenAIStreamingResponseReader_ReadAsync__Finalize]",
                            "Empty Completed Event JSON");

                        return InvokeResult<string>.FromError(
                            "Empty response from Streaming OpenAI Client",
                            "OPENAI_STREAM_EMPTY_COMPLETED");
                    }

                    // Extract the inner 'response' object from the completed event.
                    var finalResponse = OpenAiStreamingEventHelper.ExtractCompletedResponseJson(acc.CompletedEventJson);
                    if (!finalResponse.Successful)
                    {
                        return InvokeResult<string>.FromInvokeResult(finalResponse.ToInvokeResult());
                    }

                    return InvokeResult<string>.Create(finalResponse.Result);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return InvokeResult<string>.Abort();
            }
            catch (Exception ex)
            {
                _logger.AddException("[OpenAIStreamingResponseReader_ReadAsync__Exception]", ex);
                return InvokeResult<string>.FromError(
                    "Unexpected exception while reading streaming response.",
                    "OPENAI_STREAM_EXCEPTION");
            }
        }

        /// <summary>
        /// Processes the buffered SSE event:
        /// - publishes any delta text
        /// - captures completed response payload
        /// - clears accumulator state
        /// </summary>
        private async Task FlushAsync(SseAccumulator acc, string sessionId, CancellationToken ct)
        {
            if (acc.Data.Length == 0)
            {
                return;
            }

            var dataJson = acc.Data.ToString();
            var analyzed = OpenAiStreamingEventHelper.AnalyzeEventPayload(acc.CurrentEvent, dataJson);

            // Delta text is streamed immediately to UI + diagnostics.
            if (!string.IsNullOrEmpty(analyzed.DeltaText))
            {
                await _streamingUi.AddPartialAsync(analyzed.DeltaText, ct);
            }

            // Completed event carries the final response payload.
            if (string.Equals(analyzed.EventType, "response.completed", StringComparison.OrdinalIgnoreCase))
            {
                acc.CompletedEventJson = dataJson;
            }

            acc.Data.Clear();
            acc.CurrentEvent = null;
        }

        /// <summary>
        /// Holds state for the currently buffered SSE event.
        /// </summary>
        private sealed class SseAccumulator
        {
            public string CurrentEvent { get; set; }
            public StringBuilder Data { get; } = new StringBuilder();
            public string CompletedEventJson { get; set; }
        }
    }
}
