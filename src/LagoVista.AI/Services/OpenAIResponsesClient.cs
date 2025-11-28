using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Helpers;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// OpenAI implementation of ILLMClient using the Responses API (/v1/responses).
    ///
    /// Supports optional streaming-style narration via the notification pipeline
    /// when a sessionId is supplied. The orchestrator remains unaware of these
    /// events; they are an implementation detail keyed by the Aptix session id.
    /// </summary>
    public class OpenAIResponsesClient : ILLMClient
    {
        private readonly IOpenAISettings _openAiSettings;
        private readonly IAdminLogger _adminLogger;
        private readonly INotificationPublisher _notificationPublisher;

        public OpenAIResponsesClient(IOpenAISettings openAiSettings, IAdminLogger adminLogger, INotificationPublisher notificationPublisher)
        {
            _openAiSettings = openAiSettings ?? throw new ArgumentNullException(nameof(openAiSettings));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _notificationPublisher = notificationPublisher ?? throw new ArgumentNullException(nameof(notificationPublisher));
        }

        public async Task<InvokeResult<AgentExecuteResponse>> GetAnswerAsync(
            AgentContext agentContext,
            ConversationContext conversationContext,
            AgentExecuteRequest executeRequest,
            string ragContextBlock,
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            if (agentContext == null) throw new ArgumentNullException(nameof(agentContext));
            if (conversationContext == null) throw new ArgumentNullException(nameof(conversationContext));
            if (executeRequest == null) throw new ArgumentNullException(nameof(executeRequest));

            if (string.IsNullOrWhiteSpace(executeRequest.Instruction))
            {
                return InvokeResult<AgentExecuteResponse>.FromError("Instruction is required for LLM call.");
            }

            var baseUrl = _openAiSettings.OpenAIUrl;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return InvokeResult<AgentExecuteResponse>.FromError("OpenAIUrl is not configured in IOpenAISettings.");
            }

            if (string.IsNullOrWhiteSpace(agentContext.LlmApiKey))
            {
                return InvokeResult<AgentExecuteResponse>.FromError("LlmApiKey is not configured on AgentContext.");
            }

            var requestObject = ResponsesRequestBuilder.Build(conversationContext, executeRequest, ragContextBlock, true);

            var requestJson = JsonConvert.SerializeObject(requestObject);
            Console.WriteLine($"OpenAI Responses Request JSON:\r\n=====\r\n{requestJson}====\r\n");
            try
            {
                await PublishLlmEventAsync(sessionId, "LLMStarted", "in-progress", "Calling OpenAI model...", null, cancellationToken);

                using (var httpClient = CreateHttpClient(baseUrl, agentContext.LlmApiKey))
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, "/v1/responses")
                    {
                        Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
                    };

                    var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        var errorMessage = $"LLM call failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";

                        _adminLogger.AddError("[OpenAIResponsesClient_GetAnswerAsync__HTTP]", errorMessage);
                        _adminLogger.AddError("[OpenAIResponsesClient_GetAnswerAsync__Body]", errorBody);

                        OpenAIErrorResponse error = null;
                        try
                        {
                            error = JsonConvert.DeserializeObject<OpenAIErrorResponse>(errorBody);
                        }
                        catch (Exception ex)
                        {
                            // Don't let a malformed error payload blow up the HTTP error branch.
                            _adminLogger.AddException("[OpenAIResponsesClient_GetAnswerAsync__ErrorDeserialize]", ex);
                        }

                        var reasonSuffix = error != null ? $"Reason: {error}" : $"Raw: {errorBody}";

                        await PublishLlmEventAsync(
                            sessionId,
                            "LLMFailed",
                            "failed",
                            $"{errorMessage} - {reasonSuffix}",
                            null,
                            cancellationToken);

                        return InvokeResult<AgentExecuteResponse>.FromError($"{errorMessage}; {reasonSuffix}");

                    }

                    var agentResponse = await ReadStreamingResponseAsync(response, executeRequest, sessionId, cancellationToken);

                    if (!agentResponse.Successful)
                        return agentResponse;

  
                    await PublishLlmEventAsync(sessionId, "LLMCompleted", "completed", "Model response received.", null, cancellationToken);

                    return agentResponse;
                }
            }
            catch (TaskCanceledException tex) when (tex.CancellationToken == cancellationToken)
            {
                const string msg = "LLM call was cancelled.";

                _adminLogger.AddError("[OpenAIResponsesClient_GetAnswerAsync__Cancelled]", msg);
                await PublishLlmEventAsync(sessionId, "LLMCancelled", "aborted", msg, null, cancellationToken);

                return InvokeResult<AgentExecuteResponse>.FromError(msg);
            }
            catch (Exception ex)
            {
                const string msg = "Unexpected exception during LLM call.";

                _adminLogger.AddException("[OpenAIResponsesClient_GetAnswerAsync__Exception]", ex);
                await PublishLlmEventAsync(sessionId, "LLMFailed", "failed", msg, null, cancellationToken);

                return InvokeResult<AgentExecuteResponse>.FromError(msg);
            }
        }

        private async Task<InvokeResult<AgentExecuteResponse>> ReadStreamingResponseAsync(
            HttpResponseMessage httpResponse,
            AgentExecuteRequest request,
            string sessionId,
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

                // This will hold the *final* response.completed payload
                string completedEventJson = null;

                while (!reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var line = await reader.ReadLineAsync();
                    if (line == null)
                    {
                        Console.WriteLine("-null-");
                        break;
                    }

                    // Blank line => end of one SSE event
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        if (dataBuilder.Length > 0)
                        {
                            var dataJson = dataBuilder.ToString();
                            rawEventLogBuilder.AppendLine(dataJson);

                            // Capture the completed event payload so we can reconstruct
                            // the full /responses object for AgentExecuteResponseParser.
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

                Console.WriteLine($"Commpleted JSON\r\n===={completedEventJson}\r\n====");

                // If we never got any text or a completed event, treat as null/empty
                if (string.IsNullOrWhiteSpace(completedEventJson))
                {
                    _adminLogger.AddError("[OpenAIResponseClient_ReadStreamingResponseAsync_Finalize]", "Empty Completed EventJSON");

                    return InvokeResult<AgentExecuteResponse>.Create(new AgentExecuteResponse
                    {
                        Kind = string.IsNullOrWhiteSpace(fullTextBuilder.ToString()) ? "empty" : "ok",
                        ConversationContextId = request.ConversationContext?.Id,
                        AgentContextId = request.AgentContext?.Id,
                        Mode = request.Mode,
                        Text = fullTextBuilder.ToString(),
                        RawResponseJson = rawEventLogBuilder.ToString(),
                        ResponseContinuationId = responseId,
                    });
                }

                // Convert the streaming response.completed payload into the
                // non-stream /responses JSON shape expected by AgentExecuteResponseParser.
                var finalResponse = OpenAiStreamingEventHelper.ExtractCompletedResponseJson(completedEventJson); 
                if(!finalResponse.Successful)
                {
                    return InvokeResult<AgentExecuteResponse>.FromInvokeResult(finalResponse.ToInvokeResult());
                }

                var finalResponseJson = finalResponse.Result;

               
                // Parse into our normalized envelope
                var agentResponse = AgentExecuteResponseParser.Parse(finalResponseJson, request);

                if(!agentResponse.Successful)
                {
                    return agentResponse;
                }

                // Preserve the streaming raw event log and incremental text as extra diagnostics
                agentResponse.Result.RawResponseJson = rawEventLogBuilder.ToString();

                // If the parser did not see text (e.g., only tool calls), we can still
                // fill in the visible text from the streaming builder as a convenience.
                if (string.IsNullOrWhiteSpace(agentResponse.Result.Text) && fullTextBuilder.Length > 0)
                {
                    agentResponse.Result.Text = fullTextBuilder.ToString();
                }

                // If we got a responseId earlier (e.g., from ProcessSseEventAsync),
                // prefer that as the continuation id if the parser did not set one.
                if (string.IsNullOrWhiteSpace(agentResponse.Result.ResponseContinuationId) && !string.IsNullOrWhiteSpace(responseId))
                {
                    agentResponse.Result.ResponseContinuationId = responseId;
                }

                _adminLogger.Trace($"[OpenAIResponseClient_ReadStreamingResponseAsync_Finalize] - Built Agent response {agentResponse.Result.ResponseContinuationId}.");

                return agentResponse;
            }
        }

        private int _idx = 1;

        private async Task ProcessSseEventAsync(
          string eventName,
          string sessionId,
          string dataJson,
          StringBuilder fullTextBuilder,
          Action<string> setResponseId,
          CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dataJson)) return;

            Console.WriteLine($"Process Sse {_idx++} - {eventName} - {dataJson}");

            try
            {
                var result = OpenAiStreamingEventHelper.AnalyzeEventPayload(eventName, dataJson);

                // Handle delta text
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
                }

                // Handle response id from response.completed
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

        
        /// <summary>
        /// Publish a lightweight LLM-related event if a session id is available.
        /// This keeps narration fully optional and scoped to the LLM implementation.
        /// </summary>
        private async Task PublishLlmEventAsync(string sessionId, string stage, string status, string message, double? elapsedMs, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return;

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
                await _notificationPublisher.PublishAsync(Targets.WebSocket, Channels.Entity, sessionId, evt, NotificationVerbosity.Diagnostics);
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
}
