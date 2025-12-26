// File: ./src/LagoVista.AI.Services/OpenAIResponsesExecutor.cs
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services.OpenAI
{
    /// <summary>
    /// Executes an OpenAI /v1/responses call end-to-end and returns:
    /// - final response JSON (streaming or non-streaming), OR
    /// - a well-formed error message describing failure, OR
    /// - Abort() when the request is cancelled.
    /// </summary>
    public sealed class OpenAIResponsesExecutor : IOpenAIResponsesExecutor
    {
        private readonly IAdminLogger _logger;
        private readonly IOpenAIStreamingResponseReader _streamReader;
        private readonly IOpenAINonStreamingResponseReader _nonStreamReader;
        private readonly IOpenAISettings _settings;
        private readonly ILLMWorkflowNarrator _narrator;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IOpenAIErrorFormatter _errorFormatter;

        public OpenAIResponsesExecutor(
            IOpenAISettings settings,
            IOpenAIStreamingResponseReader streamReader,
            ILLMWorkflowNarrator narrator,
            IHttpClientFactory httpFactory,
            IOpenAINonStreamingResponseReader nonStreamReader,
            IOpenAIErrorFormatter errorFormatter,
            IAdminLogger logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _streamReader = streamReader ?? throw new ArgumentNullException(nameof(streamReader));
            _narrator = narrator ?? throw new ArgumentNullException(nameof(narrator));
            _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
            _nonStreamReader = nonStreamReader ?? throw new ArgumentNullException(nameof(nonStreamReader));
            _errorFormatter = errorFormatter ?? throw new ArgumentNullException(nameof(errorFormatter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<InvokeResult<string>> InvokeAsync(AgentPipelineContext ctx, string requestJson)
        {
            var httpClient = _httpFactory.CreateClient();
            var ct = ctx.CancellationToken;

            using (var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(new Uri(_settings.OpenAIUrl), "/v1/responses")))
            {
                httpRequest.Content = new StringContent(requestJson ?? "{}", Encoding.UTF8, "application/json");
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AgentContext.LlmApiKey);

                try
                {
                    await _narrator.ConnectingAsync(ct);

                    using (var httpResponse = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct))
                    {
                        await _narrator.ThinkingAsync(ct);

                        if (httpResponse.IsSuccessStatusCode)
                        {
                            var resultJson = ctx.Envelope.Stream
                                ? await _streamReader.ReadAsync(httpResponse, ctx.Session.Id, ct)
                                : await _nonStreamReader.ReadAsync(httpResponse, ct);

                            if (!resultJson.Successful) return resultJson;

                            await _narrator.SummarizingAsync(ct);
                            return resultJson;
                        }

                        var msg = "LLM call failed with HTTP " + (int)httpResponse.StatusCode + " (" + httpResponse.ReasonPhrase + ").";
                        _logger.AddError("[OpenAIResponsesExecutor_InvokeAsync__HTTP]", msg);

                        var suffix = await _errorFormatter.FormatAsync(httpResponse);
                        if (!string.IsNullOrWhiteSpace(suffix))
                        {
                            _logger.AddError("[OpenAIResponsesExecutor_InvokeAsync__Body]", suffix);
                            msg = msg + " " + suffix;
                        }

                        return InvokeResult<string>.FromError(msg, "OPENAI_EXECUTOR_HTTP_ERROR");
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return InvokeResult<string>.Abort();
                }
                catch (Exception ex)
                {
                    _logger.AddException("[OpenAIResponsesExecutor_InvokeAsync__Exception]", ex);
                    return InvokeResult<string>.FromError("Unexpected exception during LLM call.", "OPENAI_EXECUTOR_HTTP_EXCEPTION");
                }
            }
        }
    }
}
