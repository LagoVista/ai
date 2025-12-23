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
    public class OpenAIResponsesClient : ILLMClient 
    {
        private readonly IOpenAISettings _openAiSettings;
        private readonly IAdminLogger _adminLogger;
        private readonly IServerToolUsageMetadataProvider _metaUsageProvider;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly IServerToolSchemaProvider _toolSchemaProvider;
        private readonly IAgentStreamingContext _agentStreamingContext;
        private readonly IResponsesRequestBuilder _responsesRequestBuilder;

        public bool UseStreaming { get; set; } = true;

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

        public OpenAIResponsesClient(
            IOpenAISettings openAiSettings,
            IAdminLogger adminLogger,
            IServerToolUsageMetadataProvider usageProvider,
            INotificationPublisher notificationPublisher,
            IServerToolSchemaProvider toolSchemaProvider,
            IResponsesRequestBuilder responsesRequestBuilder,
            IAgentStreamingContext agentStreamingContext)
        {
            _openAiSettings = openAiSettings ?? throw new ArgumentNullException(nameof(openAiSettings));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _notificationPublisher = notificationPublisher ?? throw new ArgumentNullException(nameof(notificationPublisher));
            _metaUsageProvider = usageProvider ?? throw new ArgumentNullException(nameof(usageProvider));
            _toolSchemaProvider = toolSchemaProvider ?? throw new ArgumentNullException(nameof(toolSchemaProvider));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
            _responsesRequestBuilder = responsesRequestBuilder ?? throw new ArgumentNullException(nameof(responsesRequestBuilder));
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(
            AgentPipelineContext ctx)
        {
            if (ctx == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "AgentPipelineContext cannot be null.",
                    "OPENAI_CLIENT_NULL_CONTEXT");
            }

            if (ctx.AgentContext == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "AgentContext is required for LLM call.",
                    "OPENAI_CLIENT_MISSING_AGENT_CONTEXT");
            }

            if (ctx.ConversationContext == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "ConversationContext is required for LLM call.",
                    "OPENAI_CLIENT_MISSING_CONVERSATION_CONTEXT");
            }

            if (ctx.Request == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "AgentExecuteRequest is required for LLM call.",
                    "OPENAI_CLIENT_MISSING_REQUEST");
            }

            if (string.IsNullOrWhiteSpace(ctx.Request.Instruction))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "Instruction is required for LLM call.",
                    "OPENAI_CLIENT_MISSING_INSTRUCTION");
            }

            // Effective session id
            var sessionId = !string.IsNullOrWhiteSpace(ctx.SessionId)
                ? ctx.SessionId
                : ctx.Request.SessionId;

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "SessionId (session id) is required for LLM call.",
                    "OPENAI_CLIENT_MISSING_CONVERSATION_ID");
            }

            var baseUrl = _openAiSettings.OpenAIUrl;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "OpenAIUrl is not configured in IOpenAISettings.",
                    "OPENAI_CLIENT_MISSING_OPENAI_URL");
            }

            if (string.IsNullOrWhiteSpace(ctx.AgentContext.LlmApiKey))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "LlmApiKey is not configured on AgentContext.",
                    "OPENAI_CLIENT_MISSING_API_KEY");
            }

            var agentContext = ctx.AgentContext;
            var conversationContext = ctx.ConversationContext;
            var executeRequest = ctx.Request;
            var ragContextBlock = ctx.RagContextBlock ?? string.Empty;

            // Ensure system prompts list exists
            if (conversationContext.SystemPrompts == null)
            {
                conversationContext.SystemPrompts = new List<string>();
            }

            var mode = agentContext.AgentModes != null
                ? agentContext.AgentModes.SingleOrDefault(m => m.Key == executeRequest.Mode)
                : null;

            if (mode == null)
            {
                throw new RecordNotFoundException(nameof(AgentMode), executeRequest.Mode);
            }

            var tools = (mode.AssociatedToolIds == null ? new List<string>() : mode.AssociatedToolIds.ToList());

            // Global tools always available
            tools.Add(ModeChangeTool.ToolName);
            tools.Add(AgentListModesTool.ToolName);
            tools.Add(SessionCheckpointListTool.ToolName);
            tools.Add(SessionCheckpointRestoreTool.ToolName);
            tools.Add(SessionCheckpointSetTool.ToolName);
            tools.Add(SessionMemoryListTool.ToolName);
            tools.Add(SessionMemoryRecallTool.ToolName);
            tools.Add(SessionMemoryStoreTool.ToolName);
            tools.Add(FetchWebPageTool.ToolName);

            var toolUsageBlock = _metaUsageProvider.GetToolUsageMetadata(tools.ToArray());

            // Schemas (ok if provider returns null; serialize(null) => "null")
            executeRequest.ToolsJson = JsonConvert.SerializeObject(_toolSchemaProvider.GetToolSchemas(tools));

            // Mode system prompt
            conversationContext.SystemPrompts.Add(agentContext.BuildSystemPrompt(executeRequest.Mode));

            var requestObject = _responsesRequestBuilder.Build(conversationContext, executeRequest, ragContextBlock, toolUsageBlock);

            var requestJson = JsonConvert.SerializeObject(requestObject);
            _adminLogger.Trace("[OpenAIResponsesClient__ExecuteAsync] Call LLM with JSON\r\n=====\r\n" + requestJson + "\r\n====");

            try
            {
                await PublishLlmEventAsync(sessionId, "LLMStarted", "in-progress", "Calling OpenAI model...", null, ctx.CancellationToken);

                using (var httpClient = CreateHttpClient(baseUrl, agentContext.LlmApiKey))
                {
                    var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/responses")
                    {
                        Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
                    };

                    var rnd = new Random();
                    await _agentStreamingContext.AddWorkflowAsync(connectingMessage[rnd.Next(0, connectingMessage.Length - 1)], ctx.CancellationToken);

                    var sw = Stopwatch.StartNew();
                    var httpResponse = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ctx.CancellationToken);

                    await _agentStreamingContext.AddWorkflowAsync(thinkingMessges[rnd.Next(0, thinkingMessges.Length - 1)], ctx.CancellationToken);

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        var errorBody = await httpResponse.Content.ReadAsStringAsync();
                        var errorMessage = "LLM call failed with HTTP " + (int)httpResponse.StatusCode + " (" + httpResponse.ReasonPhrase + ").";

                        _adminLogger.AddError("[OpenAIResponsesClient_ExecuteAsync__HTTP]", errorMessage);
                        _adminLogger.AddError("[OpenAIResponsesClient_ExecuteAsync__Body]", errorBody);

                        OpenAIErrorResponse error = null;
                        try
                        {
                            error = JsonConvert.DeserializeObject<OpenAIErrorResponse>(errorBody);
                        }
                        catch (Exception ex)
                        {
                            _adminLogger.AddException("[OpenAIResponsesClient_ExecuteAsync__ErrorDeserialize]", ex);
                        }

                        var reasonSuffix = error != null ? "Reason: " + error : "Raw: " + errorBody;

                        await PublishLlmEventAsync(sessionId, "LLMFailed", "failed", errorMessage + " - " + reasonSuffix, null, ctx.CancellationToken);

                        return InvokeResult<AgentPipelineContext>.FromError(
                            errorMessage + "; " + reasonSuffix,
                            "OPENAI_CLIENT_HTTP_ERROR");
                    }

                    InvokeResult<AgentExecuteResponse> agentResponse;

                    if (UseStreaming)
                    {
                        agentResponse = await ReadStreamingResponseAsync(httpResponse, executeRequest, sessionId, sw, ctx.CancellationToken);
                    }
                    else
                    {
                        agentResponse = await ReadNonStreamingResponseAsync(httpResponse, executeRequest, sw, ctx.CancellationToken);
                    }

                    await _agentStreamingContext.AddWorkflowAsync("got it give me a minute to summarize...", ctx.CancellationToken);

                    if (!agentResponse.Successful)
                    {
                        return InvokeResult<AgentPipelineContext>.FromInvokeResult(agentResponse.ToInvokeResult());
                    }

                    if (ctx.CancellationToken.IsCancellationRequested)
                    {
                        return InvokeResult<AgentPipelineContext>.Abort();
                    }

                    await PublishLlmEventAsync(sessionId, "LLMCompleted", "completed", "Model response received.", null, ctx.CancellationToken);

                    ctx.Response = agentResponse.Result;
                    return InvokeResult<AgentPipelineContext>.Create(ctx);
                }
            }
            catch (TaskCanceledException tex) when (tex.CancellationToken == ctx.CancellationToken)
            {
                const string msg = "LLM call was cancelled.";

                _adminLogger.AddError("[OpenAIResponsesClient_ExecuteAsync__Cancelled]", msg);
                await PublishLlmEventAsync(sessionId, "LLMCancelled", "aborted", msg, null, ctx.CancellationToken);

                return InvokeResult<AgentPipelineContext>.FromError(msg, "OPENAI_CLIENT_CANCELLED");
            }
            catch (Exception ex)
            {
                const string msg = "Unexpected exception during LLM call.";

                _adminLogger.AddException("[OpenAIResponsesClient_ExecuteAsync__Exception]", ex);
                await PublishLlmEventAsync(sessionId, "LLMFailed", "failed", msg, null, ctx.CancellationToken);

                return InvokeResult<AgentPipelineContext>.FromError(msg, "OPENAI_CLIENT_EXCEPTION");
            }
        }

        private async Task<InvokeResult<AgentExecuteResponse>> ReadNonStreamingResponseAsync(
            HttpResponseMessage httpResponse,
            AgentExecuteRequest request,
            Stopwatch sw,
            CancellationToken cancellationToken)
        {
            var json = await httpResponse.Content.ReadAsStringAsync();

            _adminLogger.Trace(
                "[OpenAIResponsesClient_ReadNonStreamingResponseAsync] Agent Response in " + sw.Elapsed.TotalSeconds + " seconds. JSON\r\n====\r\n" + json + "\r\n====");

            if (string.IsNullOrWhiteSpace(json))
            {
                _adminLogger.AddError(
                    "[OpenAIResponsesClient_ReadNonStreamingResponseAsync_Finalize]",
                    "Empty response JSON.");

                return InvokeResult<AgentExecuteResponse>.Create(new AgentExecuteResponse
                {
                    Kind = "empty",
                    ConversationContextId = request.ConversationContext != null ? request.ConversationContext.Id : null,
                    AgentContextId = request.AgentContext != null ? request.AgentContext.Id : null,
                    Mode = request.Mode,
                    Text = string.Empty,
                    RawResponseJson = json,
                    ResponseContinuationId = null,
                });
            }

            var agentResponse = AgentExecuteResponseParser.Parse(json, request);
            if (!agentResponse.Successful)
            {
                return agentResponse;
            }

            agentResponse.Result.RawResponseJson = json;

            _adminLogger.Trace(
                "[OpenAIResponsesClient_ReadNonStreamingResponseAsync] Parsed Agent Response in " + sw.Elapsed.TotalSeconds + " seconds.");

            return agentResponse;
        }

        private async Task<InvokeResult<AgentExecuteResponse>> ReadStreamingResponseAsync(
            HttpResponseMessage httpResponse,
            AgentExecuteRequest request,
            string sessionId,
            Stopwatch sw,
            CancellationToken cancellationToken)
        {
            using (var stream = await httpResponse.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
                var fullTextBuilder = new StringBuilder();
                var rawEventLogBuilder = new StringBuilder();
                string responseId = null;

                string currentEvent = null;
                var dataBuilder = new StringBuilder();

                string completedEventJson = null;

                while (!reader.EndOfStream)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return InvokeResult<AgentExecuteResponse>.Abort();
                    }

                    var line = await reader.ReadLineAsync();
                    if (line == null)
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        if (dataBuilder.Length > 0)
                        {
                            var dataJson = dataBuilder.ToString();
                            rawEventLogBuilder.AppendLine(dataJson);

                            if (string.Equals(currentEvent, "response.completed", StringComparison.OrdinalIgnoreCase))
                            {
                                completedEventJson = dataJson;
                            }

                            await ProcessSseEventAsync(
                                currentEvent,
                                sessionId,
                                dataJson,
                                fullTextBuilder,
                                value => responseId = value ?? responseId,
                                cancellationToken);

                            dataBuilder.Clear();
                            currentEvent = null;
                        }

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
                            break;
                        }

                        dataBuilder.AppendLine(dataPart);
                    }
                }

                _adminLogger.Trace(
                    "[OpenAIResponsesClient_ReadStreamingResponseAsync] CompletedEvent JSON\r\n====<<<\r\n" +
                    completedEventJson + "\r\n====<<< in " + sw.Elapsed.TotalSeconds + " seconds");

                if (string.IsNullOrWhiteSpace(completedEventJson))
                {
                    _adminLogger.AddError("[OpenAIResponsesClient_ReadStreamingResponseAsync_Finalize]", "Empty Completed EventJSON");

                    return InvokeResult<AgentExecuteResponse>.Create(new AgentExecuteResponse
                    {
                        Kind = string.IsNullOrWhiteSpace(fullTextBuilder.ToString()) ? "empty" : "ok",
                        ConversationContextId = request.ConversationContext != null ? request.ConversationContext.Id : null,
                        AgentContextId = request.AgentContext != null ? request.AgentContext.Id : null,
                        Mode = request.Mode,
                        Text = fullTextBuilder.ToString(),
                        RawResponseJson = rawEventLogBuilder.ToString(),
                        ResponseContinuationId = responseId,
                    });
                }

                var finalResponse = OpenAiStreamingEventHelper.ExtractCompletedResponseJson(completedEventJson);
                if (!finalResponse.Successful)
                {
                    return InvokeResult<AgentExecuteResponse>.FromInvokeResult(finalResponse.ToInvokeResult());
                }

                var finalResponseJson = finalResponse.Result;

                var agentResponse = AgentExecuteResponseParser.Parse(finalResponseJson, request);
                if (!agentResponse.Successful)
                {
                    return agentResponse;
                }

                agentResponse.Result.RawResponseJson = rawEventLogBuilder.ToString();

                if (string.IsNullOrWhiteSpace(agentResponse.Result.Text) && fullTextBuilder.Length > 0)
                {
                    agentResponse.Result.Text = fullTextBuilder.ToString();
                }

                if (string.IsNullOrWhiteSpace(agentResponse.Result.ResponseContinuationId) && !string.IsNullOrWhiteSpace(responseId))
                {
                    agentResponse.Result.ResponseContinuationId = responseId;
                }

                _adminLogger.Trace(
                    "[OpenAIResponsesClient_ReadStreamingResponseAsync_Finalize] - Built Agent response " +
                    agentResponse.Result.ResponseContinuationId + ".");

                return agentResponse;
            }
        }

        private async Task ProcessSseEventAsync(
            string eventName,
            string sessionId,
            string dataJson,
            StringBuilder fullTextBuilder,
            Action<string> setResponseId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dataJson))
            {
                return;
            }

            try
            {
                var result = OpenAiStreamingEventHelper.AnalyzeEventPayload(eventName, dataJson);

                if (!string.IsNullOrEmpty(result.DeltaText))
                {
                    fullTextBuilder.Append(result.DeltaText);

                    await PublishLlmEventAsync(
                        sessionId,
                        "LLMDelta",
                        "in-progress",
                        result.DeltaText,
                        null,
                        cancellationToken);

                    await _agentStreamingContext.AddPartialAsync(result.DeltaText, cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(result.ResponseId))
                {
                    setResponseId(result.ResponseId);
                }
            }
            catch (Exception ex)
            {
                _adminLogger.AddException("[OpenAIResponsesClient_ProcessSseEventAsync__Exception]", ex);
            }
        }

        private async Task PublishLlmEventAsync(
            string sessionId,
            string stage,
            string status,
            string message,
            double? elapsedMs,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            var evt = new AptixOrchestratorEvent
            {
                SessionId = sessionId,
                TurnId = null,
                Stage = stage,
                Status = status,
                Message = message,
                ElapsedMs = elapsedMs,
                Timestamp = DateTime.UtcNow.ToJSONString()
            };

            try
            {
                await _notificationPublisher.PublishAsync(
                    Targets.WebSocket,
                    Channels.Entity,
                    sessionId,
                    evt,
                    NotificationVerbosity.Diagnostics);
            }
            catch (Exception ex)
            {
                _adminLogger.AddException("[OpenAIResponsesClient_PublishLlmEventAsync__Exception]", ex);
            }
        }

        /// <summary>
        /// Factory method for HttpClient so tests can override and inject fake handlers.
        /// </summary>
        protected virtual HttpClient CreateHttpClient(string baseUrl, string apiKey)
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(120)
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            return client;
        }
    }

    public sealed class OpenAIErrorResponse
    {
        [JsonProperty("error")]
        public OpenAIError Error { get; set; }

        public override string ToString()
        {
            if (Error == null)
            {
                return base.ToString();
            }

            var paramInfo = string.IsNullOrEmpty(Error.Param) ? "" : " (param: " + Error.Param + ")";
            var codeInfo = string.IsNullOrEmpty(Error.Code) ? "" : " (code: " + Error.Code + ")";
            return "OpenAI error: " + Error.Message + " [" + Error.Type + "]" + paramInfo + codeInfo;
        }
    }

    public sealed class OpenAIError
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("param")]
        public string Param { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }
    }
}
