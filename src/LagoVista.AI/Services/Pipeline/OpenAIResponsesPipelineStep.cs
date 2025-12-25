// File: ./src/LagoVista.AI.Services/OpenAIResponsesClient.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Helpers;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// OpenAI implementation of the LLM pipeline step using the Responses API (/v1/responses).
    ///
    /// This is now a pipeline step:
    /// - Reads inputs from AgentPipelineContext (AgentContext, ConversationContext, Request, RagContextBlock, SessionId).
    /// - Calls OpenAI
    /// - Sets ctx.Response
    /// - Returns InvokeResult&lt;AgentPipelineContext&gt;
    /// </summary>
    public class OpenAIResponsesClientPipelineStap : ILLMClient
    {
        private readonly IOpenAISettings _openAiSettings;
        private readonly IAdminLogger _adminLogger;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly IAgentStreamingContext _agentStreamingContext;
        private readonly IResponsesRequestBuilder _requestBuilder;
        private readonly IAgentExecuteResponseParser _responseParser;

        public string[] connectingMessage =
        {
            "reaching out…",
            "establishing a connection…",
            "opening a line…",
            "getting in touch…",
            "knocking on the door…",
            "tapping the shoulder…",
            "checking availability…",
            "lining things up…",
            "syncing up…",
            "setting up the link…",
            "spinning up a connection…",
            "calling it in…"
        };

        public string[] thinkingMessges =
        {
            "let me mull that over…",
            "one sec—connecting the dots…",
            "calling in a second opinion…",
            "asking my inner narrator…",
            "running it through the gears…",
            "spinning up some thoughts…"
        };

        public OpenAIResponsesClientPipelineStap(IOpenAISettings openAiSettings, IAdminLogger adminLogger, INotificationPublisher notificationPublisher, 
             IResponsesRequestBuilder responsesRequestBuilder, IAgentExecuteResponseParser responseParser, IAgentStreamingContext agentStreamingContext)
        {
            _openAiSettings = openAiSettings ?? throw new ArgumentNullException(nameof(openAiSettings));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _notificationPublisher = notificationPublisher ?? throw new ArgumentNullException(nameof(notificationPublisher));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
            _requestBuilder = responsesRequestBuilder ?? throw new ArgumentNullException(nameof(responsesRequestBuilder));
            _responseParser = responseParser ?? throw new ArgumentNullException(nameof(responseParser));
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx)
        {
            if (ctx == null) { return InvokeResult<AgentPipelineContext>.FromError("AgentPipelineContext cannot be null.", "OPENAI_CLIENT_NULL_CONTEXT"); }
            
            var baseUrl = _openAiSettings.OpenAIUrl;
            if (string.IsNullOrWhiteSpace(baseUrl)) { return InvokeResult<AgentPipelineContext>.FromError("OpenAIUrl is not configured in IOpenAISettings.", "OPENAI_CLIENT_MISSING_OPENAI_URL"); }
            if (string.IsNullOrWhiteSpace(ctx.AgentContext.LlmApiKey)) { return InvokeResult<AgentPipelineContext>.FromError("LlmApiKey is not configured on AgentContext.", "OPENAI_CLIENT_MISSING_API_KEY"); }

            var agentContext = ctx.AgentContext;
            
            var requestObject = await _requestBuilder.BuildAsync(ctx);
            var requestJson = JsonConvert.SerializeObject(requestObject);
            _adminLogger.Trace("[OpenAIResponsesClient__ExecuteAsync] Call LLM with JSON\r\n=====\r\n" + requestJson + "\r\n====");

            try
            {
                await PublishLlmEventAsync(ctx.SessionId, "LLMStarted", "in-progress", "Calling OpenAI model...", null);

                using (var httpClient = CreateHttpClient(baseUrl, agentContext.LlmApiKey))
                using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/responses") { Content = new StringContent(requestJson, Encoding.UTF8, "application/json") })
                {

                    var rnd = new Random();
                    await _agentStreamingContext.AddWorkflowAsync(connectingMessage[rnd.Next(connectingMessage.Length)], ctx.CancellationToken);

                    var sw = Stopwatch.StartNew();
                    using (var httpResponse = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ctx.CancellationToken))
                    {
                        await _agentStreamingContext.AddWorkflowAsync(thinkingMessges[rnd.Next(thinkingMessges.Length)], ctx.CancellationToken);

                        if (!httpResponse.IsSuccessStatusCode)
                        {
                            var errorBody = await httpResponse.Content.ReadAsStringAsync();
                            var errorMessage = "LLM call failed with HTTP " + (int)httpResponse.StatusCode + " (" + httpResponse.ReasonPhrase + ").";

                            _adminLogger.AddError("[OpenAIResponsesClient_ExecuteAsync__HTTP]", errorMessage);
                            _adminLogger.AddError("[OpenAIResponsesClient_ExecuteAsync__Body]", errorBody);

                            OpenAIErrorResponse error = null;
                            try { error = JsonConvert.DeserializeObject<OpenAIErrorResponse>(errorBody); }
                            catch (Exception ex) { _adminLogger.AddException("[OpenAIResponsesClient_ExecuteAsync__ErrorDeserialize]", ex); }

                            var reasonSuffix = error != null ? "Reason: " + error : "Raw: " + errorBody;

                            await PublishLlmEventAsync(ctx.SessionId, "LLMFailed", "failed", errorMessage + " - " + reasonSuffix, null);
                            return InvokeResult<AgentPipelineContext>.FromError(errorMessage + "; " + reasonSuffix, "OPENAI_CLIENT_HTTP_ERROR");
                        }

                        var agentResponse = ctx.Request.Streaming ?
                           await ReadStreamingResponseAsync(httpResponse, ctx, sw, ctx.CancellationToken) :
                           await ReadNonStreamingResponseAsync(httpResponse, ctx, sw, ctx.CancellationToken);

                        await _agentStreamingContext.AddWorkflowAsync("got it give me a minute to summarize...", ctx.CancellationToken);

                        if (!agentResponse.Successful) { return InvokeResult<AgentPipelineContext>.FromInvokeResult(agentResponse.ToInvokeResult()); }
                        if (ctx.CancellationToken.IsCancellationRequested) { return InvokeResult<AgentPipelineContext>.Abort(); }

                        await PublishLlmEventAsync(ctx.SessionId, "LLMCompleted", "completed", "Model response received.", null);

                        return agentResponse;
                    }
                }
            }
            catch (TaskCanceledException tex) when (tex.CancellationToken == ctx.CancellationToken)
            {
                const string msg = "LLM call was cancelled.";

                _adminLogger.AddError("[OpenAIResponsesClient_ExecuteAsync__Cancelled]", msg);
                await PublishLlmEventAsync(ctx.SessionId, "LLMCancelled", "aborted", msg, null);

                return InvokeResult<AgentPipelineContext>.FromError(msg, "OPENAI_CLIENT_CANCELLED");
            }
            catch (Exception ex)
            {
                const string msg = "Unexpected exception during LLM call.";

                _adminLogger.AddException("[OpenAIResponsesClient_ExecuteAsync__Exception]", ex);
                await PublishLlmEventAsync(ctx.SessionId, "LLMFailed", "failed", msg, null);

                return InvokeResult<AgentPipelineContext>.FromError(msg, "OPENAI_CLIENT_EXCEPTION");
            }
        }

        private async Task<InvokeResult<AgentPipelineContext>> ReadNonStreamingResponseAsync(HttpResponseMessage httpResponse, AgentPipelineContext ctx, Stopwatch sw, CancellationToken cancellationToken)
        {
            var json = await httpResponse.Content.ReadAsStringAsync();

            _adminLogger.Trace("[OpenAIResponsesClient_ReadNonStreamingResponseAsync] Agent Response in " + sw.Elapsed.TotalSeconds + " seconds. JSON\r\n====\r\n" + json + "\r\n====");

            if (string.IsNullOrWhiteSpace(json))
            {
                _adminLogger.AddError("[OpenAIResponsesClient_ReadNonStreamingResponseAsync_Finalize]", "Empty response JSON.");

                return InvokeResult<AgentPipelineContext>.FromError("Empty response JSON.");
            }

            var agentResponse = await _responseParser.ParseAsync(ctx, json);
            if (!agentResponse.Successful) { return agentResponse; }

            _adminLogger.Trace("[OpenAIResponsesClient_ReadNonStreamingResponseAsync] Parsed Agent Response in " + sw.Elapsed.TotalSeconds + " seconds.");

            return agentResponse;
        }

        private async Task<InvokeResult<AgentPipelineContext>> ReadStreamingResponseAsync(HttpResponseMessage httpResponse, AgentPipelineContext ctx, Stopwatch sw, CancellationToken cancellationToken)
        {
            using (var stream = await httpResponse.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
                var fullTextBuilder = new StringBuilder();
                
                string currentEvent = null;
                var dataBuilder = new StringBuilder();
                String responseId = null;
                string completedEventJson = null;

                while (!reader.EndOfStream)
                {
                    if (cancellationToken.IsCancellationRequested) { return InvokeResult<AgentPipelineContext>.Abort(); }

                    var line = await reader.ReadLineAsync();
                    if (line == null) { break; }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        if (dataBuilder.Length > 0)
                        {
                            var dataJson = dataBuilder.ToString();
              
                            if (string.Equals(currentEvent, "response.completed", StringComparison.OrdinalIgnoreCase)) { completedEventJson = dataJson; }

                            await ProcessSseEventAsync(currentEvent, ctx.SessionId, dataJson, fullTextBuilder, value => responseId = value ?? responseId, cancellationToken);

                            dataBuilder.Clear();
                            currentEvent = null;
                        }

                        continue;
                    }

                    if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase)) { currentEvent = line.Substring("event:".Length).Trim(); continue; }

                    if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        var dataPart = line.Substring("data:".Length).Trim();
                        if (string.Equals(dataPart, "[DONE]", StringComparison.Ordinal)) { break; }

                        dataBuilder.AppendLine(dataPart);
                    }
                }

                _adminLogger.Trace("[OpenAIResponsesClient_ReadStreamingResponseAsync] CompletedEvent JSON\r\n====<<<\r\n" + completedEventJson + "\r\n====<<< in " + sw.Elapsed.TotalSeconds + " seconds");

                if (string.IsNullOrWhiteSpace(completedEventJson))
                {
                    _adminLogger.AddError("[OpenAIResponsesClient_ReadStreamingResponseAsync_Finalize]", "Empty Completed EventJSON");

                    return InvokeResult<AgentPipelineContext>.FromError("Empty response from Streaming OpenAI Client");
                }

                var finalResponse = OpenAiStreamingEventHelper.ExtractCompletedResponseJson(completedEventJson);
                if (!finalResponse.Successful) { return InvokeResult<AgentPipelineContext>.FromInvokeResult(finalResponse.ToInvokeResult()); }

                var finalResponseJson = finalResponse.Result;

                var agentResponse = await _responseParser.ParseAsync(ctx, finalResponseJson);
                if (!agentResponse.Successful) { return agentResponse; }

                _adminLogger.Trace("[OpenAIResponsesClient_ReadStreamingResponseAsync_Finalize] - Built Agent response ");

                return agentResponse;
            }
        }

        private async Task ProcessSseEventAsync(string eventName, string sessionId, string dataJson, StringBuilder fullTextBuilder, Action<string> setResponseId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dataJson)) { return; }

            try
            {
                var result = OpenAiStreamingEventHelper.AnalyzeEventPayload(eventName, dataJson);

                if (!string.IsNullOrEmpty(result.DeltaText))
                {
                    fullTextBuilder.Append(result.DeltaText);

                    await PublishLlmEventAsync(sessionId, "LLMDelta", "in-progress", result.DeltaText, null);

                    await _agentStreamingContext.AddPartialAsync(result.DeltaText, cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(result.ResponseId)) { setResponseId(result.ResponseId); }
            }
            catch (Exception ex)
            {
                _adminLogger.AddException("[OpenAIResponsesClient_ProcessSseEventAsync__Exception]", ex);
            }
        }

        private async Task PublishLlmEventAsync(string sessionId, string stage, string status, string message, double? elapsedMs)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) { return; }

            var evt = new AptixOrchestratorEvent { SessionId = sessionId, TurnId = null, Stage = stage, Status = status, Message = message, ElapsedMs = elapsedMs, Timestamp = DateTime.UtcNow.ToJSONString() };

            try { await _notificationPublisher.PublishAsync(Targets.WebSocket, Channels.Entity, sessionId, evt, NotificationVerbosity.Diagnostics); }
            catch (Exception ex) { _adminLogger.AddException("[OpenAIResponsesClient_PublishLlmEventAsync__Exception]", ex); }
        }

        /// <summary>
        /// Factory method for HttpClient so tests can override and inject fake handlers.
        /// </summary>
        protected virtual HttpClient CreateHttpClient(string baseUrl, string apiKey)
        {
            var client = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(120) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            return client;
        }
    }
}
