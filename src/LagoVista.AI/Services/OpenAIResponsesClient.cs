using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
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
    /// This implementation also has the ability to emit lightweight progress events
    /// over the notification system when a sessionId is supplied. The orchestrator
    /// remains unaware of these events; they are an implementation detail of the
    /// LLM client keyed by the Aptix session id.
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
                }
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
                    httpClient.Timeout = TimeSpan.FromSeconds(60);
                    httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", agentContext.LlmApiKey);

                    using (var content = new StringContent(requestJson, Encoding.UTF8, "application/json"))
                    {
                        var response = await httpClient.PostAsync(
                            "/v1/responses",
                            content,
                            cancellationToken);

                        var body = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            var errorMessage =
                                $"LLM call failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";

                            _adminLogger.AddError(
                                "[OpenAIResponsesClient_GetAnswerAsync__HTTP]",
                                errorMessage);
                            _adminLogger.AddError(
                                "[OpenAIResponsesClient_GetAnswerAsync__Body]",
                                body);

                            await PublishLlmEventAsync(
                                sessionId,
                                "LLMFailed",
                                "failed",
                                errorMessage,
                                null,
                                cancellationToken);

                            return InvokeResult<LLMResult>.FromError(errorMessage);
                        }

                        var llmResult = ParseResponsesPayload(body);
                        if (llmResult == null || string.IsNullOrWhiteSpace(llmResult.Text))
                        {
                            const string msg =
                                "LLM response did not contain any text output in the expected format.";

                            _adminLogger.AddError(
                                "[OpenAIResponsesClient_GetAnswerAsync__Parse]",
                                msg);
                            _adminLogger.AddError(
                                "[OpenAIResponsesClient_GetAnswerAsync__Body]",
                                body);

                            await PublishLlmEventAsync(
                                sessionId,
                                "LLMFailed",
                                "failed",
                                msg,
                                null,
                                cancellationToken);

                            return InvokeResult<LLMResult>.FromError(msg);
                        }

                        llmResult.RawResponseJson = body;

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

        private static LLMResult ParseResponsesPayload(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            try
            {
                var root = JObject.Parse(body);

                var id = (string)root["id"];

                string text = null;
                var output = root["output"] as JArray;
                if (output != null && output.Count > 0)
                {
                    var firstOutput = output[0] as JObject;
                    var contentArray = firstOutput?["content"] as JArray;
                    if (contentArray != null && contentArray.Count > 0)
                    {
                        var firstContent = contentArray[0] as JObject;
                        text = (string)firstContent?["text"]
                               ?? (string)firstContent?["content"];
                    }
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    // Fall back to a best-effort extraction if the structure changes.
                    text = (string)root["output_text"] ?? body;
                }

                return new LLMResult
                {
                    ResponseId = id,
                    Text = text
                };
            }
            catch
            {
                // If parsing fails, return a minimal result with raw body as text.
                return new LLMResult
                {
                    ResponseId = null,
                    Text = body
                };
            }
        }
    }
}
