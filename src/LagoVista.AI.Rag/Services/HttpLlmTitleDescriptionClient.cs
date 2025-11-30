using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Rag.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// HTTP implementation of <see cref="ITitleDescriptionLlmClient"/> that calls
    /// the OpenAI /v1/responses endpoint using structured outputs (json_schema) for
    /// deterministic TitleDescriptionReviewResult payloads.
    ///
    /// This class is intentionally focused on the wire protocol and JSON parsing;
    /// business rules live in the higher-level services/orchestrators.
    /// </summary>
    public class HttpLlmTitleDescriptionClient : ITitleDescriptionLlmClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOpenAISettings _settings;
        private readonly IAdminLogger _logger;

        /// <summary>
        /// Default model to use when the request does not specify one explicitly.
        /// </summary>
        public const string DefaultModel = "gpt-5.1";

        public HttpLlmTitleDescriptionClient(
            IHttpClientFactory httpClientFactory,
            IOpenAISettings settings,
            IAdminLogger logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<TitleDescriptionReviewResult> ReviewAsync(
            TitleDescriptionReviewRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var client = _httpClientFactory.CreateClient(nameof(HttpLlmTitleDescriptionClient));
            client.BaseAddress = new Uri(_settings.OpenAIUrl.TrimEnd('/'));

            if (client.DefaultRequestHeaders.Authorization == null)
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _settings.OpenAIApiKey);
            }

            var systemPrompt = BuildSystemPrompt();
            var userPayload = BuildUserPayload(request);

            var body = BuildRequestBody(request, systemPrompt, userPayload);
            var json = JsonConvert.SerializeObject(body);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/responses")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            _logger.Trace($"[HttpLlmTitleDescriptionClient_ReviewAsync] Sending structured-output request for symbol '{request.SymbolName ?? string.Empty}'.");

            using var httpResponse = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

            var responseContent = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.AddError(
                    "HttpLlmTitleDescriptionClient_ReviewAsync",
                    $"Non-success status code from OpenAI: {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}",
                    new KeyValuePair<string, string>("ResponseBody", responseContent));

                // Let the caller apply guard rails and catalog warnings.
                throw new HttpRequestException(
                    $"OpenAI /v1/responses returned status {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}.");
            }

            try
            {
                var dto = ParseStructuredResult(responseContent);
                return MapToDomainResult(dto);
            }
            catch (Exception ex)
            {
                _logger.AddException(
                    "HttpLlmTitleDescriptionClient_ReviewAsync",
                    ex,
                    new KeyValuePair<string, string>("RawResponse", responseContent));

                // Surface a parsing failure so the higher-level service can
                // treat it as a warning and keep original values.
                throw new InvalidOperationException(
                    "Failed to parse structured title/description review result from OpenAI response.",
                    ex);
            }
        }

        /// <summary>
        /// System prompt used for all title/description refinement calls.
        /// </summary>
        private static string BuildSystemPrompt()
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are an expert technical editor for a large enterprise codebase.");
            sb.AppendLine("Your job is to refine domain and model titles/descriptions/help text used in UI metadata.");
            sb.AppendLine();
            sb.AppendLine("Requirements:");
            sb.AppendLine("- Preserve the original semantic meaning.");
            sb.AppendLine("- Improve clarity, grammar, and spelling.");
            sb.AppendLine("- Keep text concise and user-facing (no internal jargon).");
            sb.AppendLine("- Do NOT invent features or behavior that are not implied by the input.");
            sb.AppendLine();
            sb.AppendLine("You must respond ONLY with a JSON object that matches the provided JSON schema.");

            return sb.ToString();
        }

        /// <summary>
        /// User payload: we serialize the entire TitleDescriptionReviewRequest as JSON
        /// so the model sees all fields without coupling this client to the request shape.
        /// </summary>
        private static string BuildUserPayload(TitleDescriptionReviewRequest request)
        {
            // We rely on the request's own JSON shape; the schema we provide to the model
            // describes ONLY the expected output, not this wrapper.
            var wrapper = new
            {
                kind = request.Kind.ToString(),
                symbolName = request.SymbolName,
                request
            };

            return JsonConvert.SerializeObject(wrapper, Formatting.None);
        }

        /// <summary>
        /// Build the /v1/responses request body using the Responses API shape and
        /// structured outputs via <c>text.format</c> with <c>type = "json_schema"</c>.
        /// </summary>
        private static object BuildRequestBody(
            TitleDescriptionReviewRequest request,
            string systemPrompt,
            string userPayload)
        {
            var model = string.IsNullOrWhiteSpace(request.Model)
                ? DefaultModel
                : request.Model;

            var schema = new
            {
                type = "object",
                properties = new
                {
                    title = new
                    {
                        type = "string",
                        description = "Refined, user-facing title for the model or domain."
                    },
                    description = new
                    {
                        type = "string",
                        description = "Refined description explaining what this model/domain represents."
                    },
                    help = new
                    {
                        anyOf = new object[]
                        {
                            new { type = "string" },
                            new { type = "null" }
                        },
                        description = "Optional help/tooltip text; may be null if no help is appropriate."
                    },
                    hasChanges = new
                    {
                        type = "boolean",
                        description = "True if you changed any of title, description, or help."
                    },
                    requiresAttention = new
                    {
                        type = "boolean",
                        description = "True if you believe a human should manually review this item."
                    },
                    warnings = new
                    {
                        type = "array",
                        description = "Optional warnings or questions for a human reviewer.",
                        items = new
                        {
                            type = "string"
                        }
                    }
                },
                required = new[] { "title", "description", "help", "hasChanges", "requiresAttention", "warnings" },
                additionalProperties = false
            };

            var body = new
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
                                text = userPayload
                            }
                        }
                    }
                },
                text = new
                {
                    format = new
                    {
                        type = "json_schema",
                        name = "title_description_review",
                        schema,
                        strict = true
                    }
                }
            };

            return body;
        }

        /// <summary>
        /// Internal DTO matching the structured-output schema.
        /// </summary>
        private sealed class ReviewResultDto
        {
            [JsonProperty("title")]
            public string Title { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("help")]
            public string Help { get; set; }

            [JsonProperty("hasChanges")]
            public bool HasChanges { get; set; }

            [JsonProperty("requiresAttention")]
            public bool RequiresAttention { get; set; }

            [JsonProperty("warnings")]
            public List<string> Warnings { get; set; } = new List<string>();
        }

        /// <summary>
        /// Parse the Responses API payload and extract the structured JSON result.
        /// Handles both the ideal <c>json</c> field and the fallback of JSON in <c>text</c>.
        /// </summary>
        private static ReviewResultDto ParseStructuredResult(string rawJson)
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

            // Different SDKs / modes may expose structured output under slightly different
            // property names. Prefer "json" when present; fall back to "parsed" or "text".
            var jsonToken = firstContent["json"] ?? firstContent["parsed"] ?? firstContent["text"];

            if (jsonToken == null)
            {
                throw new InvalidOperationException(
                    "Responses payload did not contain a 'json', 'parsed', or 'text' field for structured output.");
            }

            string jsonString;

            if (jsonToken.Type == JTokenType.String)
            {
                jsonString = jsonToken.Value<string>();
            }
            else
            {
                jsonString = jsonToken.ToString(Formatting.None);
            }

            if (string.IsNullOrWhiteSpace(jsonString))
            {
                throw new InvalidOperationException("Structured output JSON was empty.");
            }

            var dto = JsonConvert.DeserializeObject<ReviewResultDto>(jsonString);

            if (dto == null)
            {
                throw new InvalidOperationException("Failed to deserialize structured title/description review JSON.");
            }

            dto.Warnings ??= new List<string>();
            return dto;
        }

        /// <summary>
        /// Map the internal DTO into the domain-level TitleDescriptionReviewResult.
        /// </summary>
        private static TitleDescriptionReviewResult MapToDomainResult(ReviewResultDto dto)
        {
            var result = new TitleDescriptionReviewResult
            {
                Title = dto.Title,
                Description = dto.Description,
                Help = dto.Help,
                HasChanges = dto.HasChanges,
                RequiresAttention = dto.RequiresAttention
            };

            if (dto.Warnings != null)
            {
                foreach (var warning in dto.Warnings)
                {
                    if (!string.IsNullOrWhiteSpace(warning))
                    {
                        result.Warnings.Add(warning.Trim());
                    }
                }
            }

            return result;
        }
    }
}
