using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
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

        public OpenAIResponsesClient(
            IOpenAISettings openAiSettings,
            IAdminLogger adminLogger,
            INotificationPublisher notificationPublisher)
        {
            _openAiSettings = openAiSettings
                ?? throw new ArgumentNullException(nameof(openAiSettings));
            _adminLogger = adminLogger
                ?? throw new ArgumentNullException(nameof(adminLogger));
            _notificationPublisher = notificationPublisher
                ?? throw new ArgumentNullException(nameof(notificationPublisher));
        }

        public async Task<InvokeResult<LLMResult>> GetAnswerAsync(
            AgentContext agentContext,
            ConversationContext conversationContext,
            string userPrompt,
            string contextPrompt,
            string sessionId = null,
            CancellationToken cancellationToken = default)
        {
            if (agentContext == null)
            {
                throw new ArgumentNullException(nameof(agentContext));
            }

            if (conversationContext == null)
            {
                throw new ArgumentNullException(nameof(conversationContext));
            }

            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                return InvokeResult<LLMResult>.FromError(
                    "User prompt is required for LLM call.");
            }

            var baseUrl = _openAiSettings.OpenAIUrl;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return InvokeResult<LLMResult>.FromError(
                    "OpenAIUrl is not configured in IOpenAISettings.");
            }

            if (string.IsNullOrWhiteSpace(agentContext.LlmApiKey))
            {
                return InvokeResult<LLMResult>.FromError(
                    "LlmApiKey is not configured on AgentContext.");
            }

            var combinedPromptBuilder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(conversationContext.System))
            {
                combinedPromptBuilder
                    .AppendLine(conversationContext.System.Trim())
                    .AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(contextPrompt))
            {
                combinedPromptBuilder
                    .AppendLine(contextPrompt.Trim())
                    .AppendLine();
            }

            combinedPromptBuilder
                .AppendLine("User instruction:")
                .AppendLine(userPrompt.Trim());

            var combinedPrompt = combinedPromptBuilder.ToString();

            var requestObject = new
            {
                model = conversationContext.ModelName,
                input = combinedPrompt,
                temperature = (double)conversationContext.Temperature,
                response_format = new
                {
                    type = "text"
                },
                stream = true
            };

            var requestJson = Newtonsoft.Json.JsonConvert.SerializeObject(requestObject);

            try
            {
                await PublishLlmEventAsync(
                    sessionId,
                    "LLMStarted",
                    "in-progress",
                    "Calling OpenAI model...",
                    null,
                    cancellationToken);

                using (var httpClient = new HttpClient())
                {
                    httpClient.BaseAddress = new Uri(baseUrl);
                    httpClient.Timeout = TimeSpan.FromSeconds(120);
                    httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", agentContext.LlmApiKey);

                    var request = new HttpRequestMessage(HttpMethod.Post, "/v1/responses")
                    {
                        Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
                    };

                    var response = await httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        var errorMessage =
                            $"LLM call failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";

                        _adminLogger.AddError(
                            "[OpenAIResponsesClient_GetAnswerAsync__HTTP]",
                            errorMessage);
                        _adminLogger.AddError(
                            "[OpenAIResponsesClient_GetAnswerAsync__Body]",
                            errorBody);

                        await PublishLlmEventAsync(
                            sessionId,
                            "LLMFailed",
                            "failed",
                            errorMessage,
                            null,
                            cancellationToken);

                        return InvokeResult<LLMResult>.FromError(errorMessage);
                    }

                    var llmResult = await ReadStreamingResponseAsync(
                        response,
                        sessionId,
                        cancellationToken);

                    if (llmResult == null || string.IsNullOrWhiteSpace(llmResult.Text))
                    {
                        const string msg =
                            "LLM response did not contain any text output in the expected streaming format.";

                        _adminLogger.AddError(
                            "[OpenAIResponsesClient_GetAnswerAsync__ParseStreaming]",
                            msg);

                        await PublishLlmEventAsync(
                            sessionId,
                            "LLMFailed",
                            "failed",
                            msg,
                            null,
                            cancellationToken);

                        return InvokeResult<LLMResult>.FromError(msg);
                    }

                    await PublishLlmEventAsync(
                        sessionId,
                        "LLMCompleted",
                        "completed",
                        "Model response received.",
                        null,
                        cancellationToken);

                    return InvokeResult<LLMResult>.Create(llmResult);
                }
            }
            catch (TaskCanceledException tex) when (tex.CancellationToken == cancellationToken)
            {
                const string msg = "LLM call was cancelled.";
                _adminLogger.AddError(
                    "[OpenAIResponsesClient_GetAnswerAsync__Cancelled]",
                    msg);

                await PublishLlmEventAsync(
                    sessionId,
                    "LLMCancelled",
                    "aborted",
                    msg,
                    null,
                    cancellationToken);

                return InvokeResult<LLMResult>.FromError(msg);
            }
            catch (Exception ex)
            {
                const string msg = "Unexpected exception during LLM call.";
                _adminLogger.AddException(
                    "[OpenAIResponsesClient_GetAnswerAsync__Exception]",
                    ex);

                await PublishLlmEventAsync(
                    sessionId,
                    "LLMFailed",
                    "failed",
                    msg,
                    null,
                    cancellationToken);

                return InvokeResult<LLMResult>.FromError(msg);
            }
        }

        /// <summary>
        /// Read a streaming Responses API response (SSE-style) and accumulate
        /// both the full text and intermediate narration events.
        ///
        /// This implementation focuses on text deltas from events whose type or
        /// event name indicate "output_text.delta". If the upstream schema
        /// evolves, this method can be adjusted without impacting callers.
        /// </summary>
        private async Task<LLMResult> ReadStreamingResponseAsync(
            HttpResponseMessage response,
            string sessionId,
            CancellationToken cancellationToken)
        {
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
                var fullTextBuilder = new StringBuilder();
                var rawBuilder = new StringBuilder();
                string responseId = null;

                string currentEvent = null;
                var dataBuilder = new StringBuilder();

                while (!reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();
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
                            rawBuilder.AppendLine(dataJson);

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

                if (fullTextBuilder.Length == 0)
                {
                    return null;
                }

                var result = new LLMResult
                {
                    ResponseId = responseId,
                    Text = fullTextBuilder.ToString(),
                    RawResponseJson = rawBuilder.ToString()
                };

                return result;
            }
        }

        /// <summary>
        /// Process a single SSE event payload from the Responses API. Attempts to
        /// extract text deltas and response id in a schema-tolerant way and emit
        /// LLMDelta narration events for each non-empty text chunk.
        /// </summary>
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
                var root = JObject.Parse(dataJson);

                var type = (string)root["type"] ?? eventName ?? string.Empty;

                if (type.EndsWith("output_text.delta", StringComparison.OrdinalIgnoreCase))
                {
                    var deltaText =
                        (string)root["delta"]?["text"] ??
                        (string)root["output_text"]?["delta"]?["text"] ??
                        string.Empty;

                    if (!string.IsNullOrEmpty(deltaText))
                    {
                        fullTextBuilder.Append(deltaText);

                        await PublishLlmEventAsync(
                            sessionId,
                            "LLMDelta",
                            "in-progress",
                            deltaText,
                            null,
                            cancellationToken);
                    }
                }
                else if (string.Equals(type, "response.completed", StringComparison.OrdinalIgnoreCase))
                {
                    var respId = (string)root["response"]?["id"];
                    if (!string.IsNullOrWhiteSpace(respId))
                    {
                        setResponseId(respId);
                    }
                }
            }
            catch (Exception ex)
            {
                _adminLogger.AddException(
                    "[OpenAIResponsesClient_ProcessSseEventAsync__Exception]",
                    ex);
            }
        }

        /// <summary>
        /// Publish a lightweight LLM-related event if a session id is available.
        /// This keeps narration fully optional and scoped to the LLM implementation.
        /// </summary>
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
                _adminLogger.AddException(
                    "[OpenAIResponsesClient_PublishLlmEventAsync__Exception]",
                    ex);
            }
        }
    }
}
