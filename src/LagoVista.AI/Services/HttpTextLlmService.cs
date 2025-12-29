using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// HTTP implementation of ITextLlmService that calls the OpenAI-style
    /// /v1/responses endpoint and returns a plain string result.
    ///
    /// This version does NOT use structured outputs / json_schema.
    /// It simply sends developer + user messages and reads the first
    /// text chunk from the response.
    /// </summary>
    public class HttpTextLlmService : ITextLlmService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private IOpenAISettings _settings;
        private readonly IAdminLogger _logger;

        /// <summary>
        /// Default model to use when no explicit model is provided.
        /// </summary>
        public const string DefaultModel = "gpt-5.1";

        private const string HttpClientName = nameof(HttpTextLlmService);

        public HttpTextLlmService(
            IHttpClientFactory httpClientFactory,
            IAdminLogger logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<InvokeResult<string>> ExecuteAsync(
            string systemPrompt,
            string inputText,
            CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(systemPrompt, inputText, null, null, null, cancellationToken);
        }

        public Task<InvokeResult<string>> ExecuteAsync(
            IOpenAISettings openAiSettings,
            string systemPrompt,
            string inputText,
            CancellationToken cancellationToken = default)
        {
            _settings = openAiSettings;
            return ExecuteAsync(systemPrompt, inputText, null, null, null, cancellationToken);
        }

        public async Task<InvokeResult<string>> ExecuteAsync(
            string systemPrompt,
            string inputText,
            string model,
            string operationName,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            var result = new InvokeResult<string>();

            if (_settings == null)
            {
                result.AddUserError("OpenAI settings have not been provided, either provide in the constructor or overload that accepts IOpenAISettings parameter.");
                return result;
            }

            if (string.IsNullOrEmpty(_settings.OpenAIApiKey))
            {
                result.AddUserError("OpenAI settings do not have an API Key.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                result.AddUserError("System prompt must not be empty.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(inputText))
            {
                result.AddUserError("Input text must not be empty.");
                return result;
            }

            var client = _httpClientFactory.CreateClient(HttpClientName);
            client.BaseAddress = new Uri(_settings.OpenAIUrl.TrimEnd('/'));

            if (client.DefaultRequestHeaders.Authorization == null)
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _settings.OpenAIApiKey);
            }

            var effectiveModel = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;

            var requestBody = BuildRequestBody(systemPrompt, inputText, effectiveModel);
            var json = JsonConvert.SerializeObject(requestBody);

            _logger.Trace($"[{nameof(HttpTextLlmService)}__{nameof(ExecuteAsync)}__Send]\r\n===>>\r\n{json}\r\n===>>\r\n");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/responses")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var traceMsg = new StringBuilder();
            traceMsg.Append($"[{nameof(HttpTextLlmService)}__{nameof(ExecuteAsync)}] Sending text-only request.");

            if (!string.IsNullOrWhiteSpace(operationName))
            {
                traceMsg.Append($" Operation: '{operationName}'.");
            }

            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                traceMsg.Append($" CorrelationId: '{correlationId}'.");
            }

            _logger.Trace(traceMsg.ToString());

            HttpResponseMessage httpResponse;
            string responseContent;

            try
            {
                httpResponse = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
                responseContent = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.Trace($"[{nameof(HttpTextLlmService)}__{nameof(ExecuteAsync)}__Recv]\r\n[JSON.LLMTEXT]={responseContent.Replace("\r",String.Empty).Replace("\n", String.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.AddException(
                    $"[{nameof(HttpTextLlmService)}__{nameof(ExecuteAsync)}]",
                    ex,
                    new KeyValuePair<string, string>("Phase", "HTTP_REQUEST"));

                result.AddUserError($"Exception calling LLM  {ex.Message}.");
                return result;
            }

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.AddError(
                    $"[{nameof(HttpTextLlmService)}__{nameof(ExecuteAsync)}]",
                    $"Non-success status code from LLM provider: {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}",
                    new KeyValuePair<string, string>("ResponseBody", responseContent));

                result.AddUserError($"LLM provider returned a non-success status code - {responseContent}");
                return result;
            }

            try
            {
                var text = ExtractPlainText(responseContent);

                if (string.IsNullOrWhiteSpace(text))
                {
                    result.AddUserError("LLM returned empty or null text.");
                    return result;
                }

                result.Result = text;
                return result;
            }
            catch (Exception ex)
            {
                _logger.AddException(
                    $"[{nameof(HttpTextLlmService)}__{nameof(ExecuteAsync)}]",
                    ex,
                    new KeyValuePair<string, string>("RawResponse", responseContent));

                result.AddUserError("Failed to parse LLM text output.");
                return result;
            }
        }

        /// <summary>
        /// Build the request body for the /v1/responses endpoint using a developer
        /// message (system prompt) and user message (input text).
        ///
        /// NOTE: No json_schema / structured output is used here.
        /// </summary>
        private static object BuildRequestBody(string systemPrompt, string inputText, string model)
        {
            return new
            {
                model,
                input = new object[]
                {
                    new
                    {
                        role = "developer",
                        content = new object[]
                        {
                            new
                            {
                                type = "input_text",
                                text = systemPrompt
                            }
                        }
                    },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "input_text",
                                text = inputText
                            }
                        }
                    }
                },
                // Let the model return standard text output; no structured format specified.
                text = new { }
            };
        }

        /// <summary>
        /// Extracts the plain text from the Responses API payload.
        /// Expects: output[0].content[0].text to be a string.
        /// </summary>
        private static string ExtractPlainText(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                throw new ArgumentException("Responses payload was empty.", nameof(rawJson));
            }

            var root = JObject.Parse(rawJson);

            var outputArray = root["output"] as JArray
                              ?? throw new InvalidOperationException("Responses payload missing 'output' array.");

            if (outputArray.Count == 0)
            {
                throw new InvalidOperationException("Responses payload had an empty 'output' array.");
            }

            var firstItem = outputArray[0];
            var contentArray = firstItem?["content"] as JArray
                               ?? throw new InvalidOperationException("Responses payload missing 'content' array.");

            if (contentArray.Count == 0)
            {
                throw new InvalidOperationException("Responses payload had empty 'content' array.");
            }

            var firstContent = contentArray[0];
            var textToken = firstContent["text"];

            if (textToken == null || textToken.Type != JTokenType.String)
            {
                throw new InvalidOperationException(
                    "Responses payload did not contain a string 'text' field for the first content item.");
            }

            return textToken.Value<string>();
        }
    }
}
