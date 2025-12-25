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
    /// Reads a streaming /responses response (SSE) and returns the completed response JSON
    /// (inner 'response' object).
    ///
    /// Minimal + testable. Hardening comes via tests.
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

        public async Task<InvokeResult<string>> ReadAsync(HttpResponseMessage httpResponse, string sessionId, CancellationToken cancellationToken = default)
        {
            if (httpResponse == null)
            {
                return InvokeResult<string>.FromError("HttpResponseMessage is null.", "OPENAI_STREAM_NULL_RESPONSE");
            }

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return InvokeResult<string>.FromError("sessionId is required for streaming reader.", "OPENAI_STREAM_MISSING_SESSION");
            }

            try
            {
                using (var stream = await httpResponse.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    string completedEventJson = null;
                    string currentEvent = null;
                    var dataBuilder = new StringBuilder();

                    async Task ProcessBufferedEventAsync()
                    {
                        if (dataBuilder.Length == 0) return;

                        var dataJson = dataBuilder.ToString();
                        var analyzed = OpenAiStreamingEventHelper.AnalyzeEventPayload(currentEvent, dataJson);

                        if (!string.IsNullOrEmpty(analyzed.DeltaText))
                        {
                            await _events.PublishAsync(sessionId, "LLMDelta", "in-progress", analyzed.DeltaText, null, cancellationToken);
                            await _streamingUi.AddPartialAsync(analyzed.DeltaText, cancellationToken);
                        }

                        if (string.Equals(analyzed.EventType, "response.completed", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(currentEvent, "response.completed", StringComparison.OrdinalIgnoreCase))
                        {
                            completedEventJson = dataJson;
                        }

                        dataBuilder.Clear();
                        currentEvent = null;
                    }

                    while (!reader.EndOfStream)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return InvokeResult<string>.Abort();
                        }

                        var line = await reader.ReadLineAsync();
                        if (line == null) break;

                        if (string.IsNullOrWhiteSpace(line))
                        {
                            await ProcessBufferedEventAsync();
                            continue;
                        }

                        if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
                        {
                            currentEvent = line.Substring("event:".Length).Trim();
                            continue;
                        }

                        if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            var dataPart = line.Substring("data:".Length).Trim();
                            if (string.Equals(dataPart, "[DONE]", StringComparison.Ordinal))
                            {
                                // IMPORTANT: flush last buffered event before finishing.
                                await ProcessBufferedEventAsync();
                                break;
                            }

                            dataBuilder.AppendLine(dataPart);
                        }
                    }

                    // IMPORTANT: flush any trailing buffered event if stream ended without a blank line.
                    await ProcessBufferedEventAsync();

                    if (string.IsNullOrWhiteSpace(completedEventJson))
                    {
                        _logger.AddError("[OpenAIStreamingResponseReader_ReadAsync__Finalize]", "Empty Completed Event JSON");
                        return InvokeResult<string>.FromError("Empty response from Streaming OpenAI Client", "OPENAI_STREAM_EMPTY_COMPLETED");
                    }

                    var finalResponse = OpenAiStreamingEventHelper.ExtractCompletedResponseJson(completedEventJson);
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
                return InvokeResult<string>.FromError("Unexpected exception while reading streaming response.", "OPENAI_STREAM_EXCEPTION");
            }
        }
    }
}
