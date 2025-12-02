using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Rag.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// HTTP implementation of IStructuredTextLlmService that calls the OpenAI-style
    /// /v1/responses endpoint using structured outputs (json_schema) and maps the
    /// structured JSON into TResult.
    ///
    /// This class focuses on wire protocol, schema generation from TResult, and
    /// mapping into InvokeResult&lt;TResult&gt;.
    /// </summary>
    public class HttpStructuredTextLlmService : IStructuredTextLlmService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOpenAISettings _settings;
        private readonly IAdminLogger _logger;

        /// <summary>
        /// Default model to use when no explicit model is provided.
        /// </summary>
        public const string DefaultModel = "gpt-5.1";

        private const string HttpClientName = nameof(HttpStructuredTextLlmService);

        public HttpStructuredTextLlmService(
            IHttpClientFactory httpClientFactory,
            IOpenAISettings settings,
            IAdminLogger logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<InvokeResult<TResult>> ExecuteAsync<TResult>(
            string systemPrompt,
            string inputText,
            CancellationToken cancellationToken = default)
        {
            return ExecuteAsync<TResult>(systemPrompt, inputText, null, null, null, cancellationToken);
        }

        public Task<InvokeResult<string>> ExecuteAsync(
            string systemPrompt,
            string inputText,
            CancellationToken cancellationToken = default)
        {
            return ExecuteAsync<string>(systemPrompt, inputText, null, null, null, cancellationToken);
        }

        public async Task<InvokeResult<TResult>> ExecuteAsync<TResult>(
            string systemPrompt,
            string inputText,
            string model,
            string operationName,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            var result = new InvokeResult<TResult>();

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

            JObject schema;
            try
            {
                schema = BuildJsonSchemaForType(typeof(TResult));
            }
            catch (Exception ex)
            {
                _logger.AddException(
                    nameof(HttpStructuredTextLlmService),
                    ex,
                    new KeyValuePair<string, string>("SchemaType", typeof(TResult).FullName ?? string.Empty));

                result.AddUserError($"Failed to generate JSON schema for result type '{typeof(TResult).Name}'.");
                return result;
            }

            var requestBody = BuildRequestBody(systemPrompt, inputText, effectiveModel, schema, typeof(TResult));
            var json = JsonConvert.SerializeObject(requestBody);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/responses")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var traceMsg = new StringBuilder();
            traceMsg.Append("[HttpStructuredTextLlmService.ExecuteAsync] Sending structured-output request.");

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
            }
            catch (Exception ex)
            {
                _logger.AddException(
                    nameof(HttpStructuredTextLlmService),
                    ex,
                    new KeyValuePair<string, string>("Phase", "HTTP_REQUEST"));

                result.AddUserError("Error while calling LLM provider.");
                return result;
            }

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.AddError(
                    nameof(HttpStructuredTextLlmService),
                    $"Non-success status code from LLM provider: {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}",
                    new KeyValuePair<string, string>("ResponseBody", responseContent));

                result.AddUserError("LLM provider returned a non-success status code.");
                return result;
            }

            try
            {
                var structuredToken = ExtractStructuredJson(responseContent);
                var typed = structuredToken.ToObject<TResult>();

                if (typed == null)
                {
                    result.AddUserError("LLM returned empty or null result for the requested type.");
                    return result;
                }

                result.Result = typed;
                return result;
            }
            catch (Exception ex)
            {
                _logger.AddException(
                    nameof(HttpStructuredTextLlmService),
                    ex,
                    new KeyValuePair<string, string>("RawResponse", responseContent));

                result.AddUserError("Failed to parse or map LLM structured output into the requested type.");
                return result;
            }
        }

        /// <summary>
        /// Build the request body for the /v1/responses endpoint using a developer
        /// message (system prompt) and user message (input text), plus a json_schema
        /// derived from TResult.
        /// </summary>
        private static object BuildRequestBody(
            string systemPrompt,
            string inputText,
            string model,
            JObject schema,
            Type resultType)
        {
            var schemaName = $"structured_result_{resultType.Name}";

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
                text = new
                {
                    format = new
                    {
                        type = "json_schema",
                        name = schemaName,
                        schema,
                        strict = true
                    }
                }
            };
        }

        /// <summary>
        /// Extract the structured JSON from the LLM provider's response.
        /// Expects an "output" array with a "content" array and a structured
        /// field under "json", "parsed", or "text".
        /// </summary>
        private static JToken ExtractStructuredJson(string rawJson)
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

            var jsonToken = firstContent["json"] ?? firstContent["parsed"] ?? firstContent["text"];

            if (jsonToken == null)
            {
                throw new InvalidOperationException(
                    "Responses payload did not contain a 'json', 'parsed', or 'text' field for structured output.");
            }

            if (jsonToken.Type == JTokenType.String)
            {
                var jsonString = jsonToken.Value<string>();

                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    throw new InvalidOperationException("Structured output JSON was empty.");
                }

                return JToken.Parse(jsonString);
            }

            return jsonToken;
        }

        /// <summary>
        /// Build a simple JSON schema based on the shape of the given type.
        /// This is intentionally conservative and supports primitives, enums,
        /// arrays/collections, and POCOs with public readable/writable properties.
        /// </summary>
        private static JObject BuildJsonSchemaForType(Type type)
        {
            if (type == typeof(string))
            {
                return new JObject
                {
                    ["type"] = "string"
                };
            }

            if (IsNumericType(type))
            {
                return new JObject
                {
                    ["type"] = type == typeof(int) || type == typeof(long) || type == typeof(short)
                        ? "integer"
                        : "number"
                };
            }

            if (type == typeof(bool))
            {
                return new JObject
                {
                    ["type"] = "boolean"
                };
            }

            if (type.IsEnum)
            {
                return new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray(Enum.GetNames(type))
                };
            }

            if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            {
                var elementType = GetElementType(type) ?? typeof(object);
                return new JObject
                {
                    ["type"] = "array",
                    ["items"] = BuildJsonSchemaForType(elementType)
                };
            }

            // Treat as object/POCO
            var properties = new JObject();
            var required = new JArray();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                var propSchema = BuildJsonSchemaForType(prop.PropertyType);
                properties[prop.Name] = propSchema;

                if (prop.PropertyType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) == null)
                {
                    required.Add(prop.Name);
                }
            }

            var obj = new JObject
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["additionalProperties"] = false
            };

            if (required.Count > 0)
            {
                obj["required"] = required;
            }

            return obj;
        }

        private static bool IsNumericType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            return type == typeof(byte) || type == typeof(sbyte) ||
                   type == typeof(short) || type == typeof(ushort) ||
                   type == typeof(int) || type == typeof(uint) ||
                   type == typeof(long) || type == typeof(ulong) ||
                   type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal);
        }

        private static Type GetElementType(Type seqType)
        {
            if (seqType.IsArray)
            {
                return seqType.GetElementType();
            }

            if (seqType.IsGenericType)
            {
                var args = seqType.GetGenericArguments();
                if (args.Length == 1)
                {
                    return args[0];
                }
            }

            var ienum = seqType
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return ienum?.GetGenericArguments().FirstOrDefault();
        }
    }
}
