using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.ContractPacks.Quality.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;

namespace LagoVista.AI.Rag.Chunkers.Services
{

    /// <summary>
    /// Default implementation of IInterfaceSemanticEnricher.
    ///
    /// This implementation builds a strict JSON-oriented prompt from InterfaceDescription,
    /// calls an LLM over HTTP, expects a structured JSON response with enriched fields,
    /// and merges the result back into the InterfaceDescription.
    ///
    /// Notes:
    /// - Uses HttpClient injected via DI.
    /// - Assumes AgentContext exposes endpoint / key / model configuration.
    ///   Adjust property names in BuildHttpRequestAsync to match your concrete AgentContext.
    /// </summary>
    public class InterfaceSemanticEnricher : IInterfaceSemanticEnricher
    {
        private readonly HttpClient _httpClient;

        // Hard limits from IDX-063.
        private const int OverviewSummaryMaxLength = 320;
        private const int ResponsibilitiesEntryMaxLength = 120;
        private const int UsageNotesEntryMaxLength = 120;
        private const int LinkageSummaryMaxLength = 320;
        private const int MethodSummaryMaxLength = 120;

        public InterfaceSemanticEnricher()
        {
            _httpClient = new HttpClient();
        }

        public async Task<InvokeResult<InterfaceDescription>> EnrichAsync(
            InterfaceDescription description,
            IngestionConfig config,
            CancellationToken cancellationToken = default)
        {
            var result = new InvokeResult<InterfaceDescription>();

            if (description == null)
            {
                result.AddUserError("InterfaceDescription cannot be null.");
                return result;
            }

            if (config == null)
            {
                result.AddUserError("IngestionConfig cannot be null.");
                return result;
            }

            try
            {
                var prompt = BuildPrompt(description);

                var llmResponseJson = await CallLlmAsync(
                    config,
                    prompt,
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(llmResponseJson))
                {
                    result.AddSystemError("LLM returned an empty response for interface enrichment.");
                    return result;
                }

                var enrichment = ParseLlmResponse(llmResponseJson, result.ToInvokeResult());
                if (!result.Successful)
                {
                    return result;
                }

                ApplyEnrichment(description, enrichment);

                result.Result = description;
                return result;
            }
            catch (OperationCanceledException)
            {
                result.AddSystemError("Interface enrichment was cancelled.");
                return result;
            }
            catch (Exception ex)
            {
                result.AddSystemError($"Unexpected error during interface enrichment: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Builds a compact JSON-style prompt that describes the interface and the enrichment
        /// contract to the LLM. The model is asked to return strict JSON matching
        /// InterfaceEnrichmentResponse.
        /// </summary>
        private static string BuildPrompt(InterfaceDescription description)
        {
            // Anonymous projection to keep prompt small and stable.
            var promptPayload = new
            {
                interfaceDescription = new
                {
                    description.InterfaceName,
                    description.FullName,
                    description.IsGeneric,
                    description.GenericArity,
                    description.BaseInterfaces,
                    description.Role,
                    Methods = description.Methods?.Select(m => new
                    {
                        m.Name,
                        m.ReturnType,
                        m.IsAsync,
                        Parameters = m.Parameters?.Select(p => new
                        {
                            p.Name,
                            p.Type,
                            p.IsOptional,
                            p.DefaultValue
                        }).ToList(),
                        // Raw XML summary may be present; model can use it, but will rewrite
                        // into SemanticSummary.
                        XmlSummary = m.Summary
                    }).ToList(),
                    description.ImplementedBy,
                    description.UsedByControllers
                }
            };

            var descriptionJson = JsonConvert.SerializeObject(promptPayload, Formatting.None);

            var sb = new StringBuilder();

            sb.AppendLine("You are a deterministic assistant that enriches C# interface metadata for vector embeddings.");
            sb.AppendLine("You will receive a JSON object describing an interface.");
            sb.AppendLine("You must respond ONLY with JSON matching this schema:");
            sb.AppendLine("{");
            sb.AppendLine("  \"overviewSummary\": string, // <= 320 chars");
            sb.AppendLine("  \"responsibilities\": string[], // each <= 120 chars");
            sb.AppendLine("  \"usageNotes\": string[], // each <= 120 chars");
            sb.AppendLine("  \"linkageSummary\": string, // <= 320 chars or empty string");
            sb.AppendLine("  \"methods\": { \"MethodName\": string /* semantic summary <= 120 chars */ } ");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- Use neutral, factual language.");
            sb.AppendLine("- Do NOT speculate about persistence, networking, security, or performance.");
            sb.AppendLine("- Base your text ONLY on names, signatures, XML summaries, and linkage info.");
            sb.AppendLine("- Do NOT include Markdown or HTML.");
            sb.AppendLine("- Do NOT add comments or any text outside the JSON.");
            sb.AppendLine("- Be deterministic: same input JSON must produce the same output.");
            sb.AppendLine();
            sb.AppendLine("Input JSON:");
            sb.AppendLine(descriptionJson);

            return sb.ToString();
        }

        /// <summary>
        /// Calls the LLM using HttpClient and AgentContext configuration.
        ///
        /// NOTE: Adjust the property names used on AgentContext to match your concrete
        /// implementation (e.g. base URL, API key, model name, etc.). This is a template
        /// and may need to be wired into your existing OpenAIResponsesClient or equivalent.
        /// </summary>
        private async Task<string> CallLlmAsync(
            IngestionConfig agentContext,
            string prompt,
            CancellationToken cancellationToken)
        {
            // These property names are placeholders. Update to match your AgentContext.
            // For example, you might have config.Settings.ApiKey, config.ModelName, etc.

            var endpoint = "https://api.openai.com/v1/responses"; // TODO: adjust to your model
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new InvalidOperationException("AgentContext.LLMEndpointUrl is not configured.");
            }

            var apiKey = agentContext.Embeddings.ApiKey; // TODO: adjust to your model
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("AgentContext.ApiKey is not configured.");
            }

            var modelName = "gpt-5.1";

            var requestBody = new
            {
                model = modelName,
                // This payload is intentionally generic; adapt to your chosen LLM API.
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };

            var json = JsonConvert.SerializeObject(requestBody, Formatting.None);
            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                using (var response = await _httpClient.SendAsync(request, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync(cancellationToken);
                }
            }
        }

        /// <summary>
        /// Parses the LLM JSON response into a strongly typed enrichment object.
        /// </summary>
        private static InterfaceEnrichmentResponse ParseLlmResponse(
            string llmResponseJson,
            InvokeResult result)
        {
            try
            {
                // Many chat APIs wrap the content; this assumes you've already extracted
                // the tool / message content as raw JSON. If your API returns a different
                // envelope, adapt this to pull out the JSON string first.
                var enrichment = JsonConvert.DeserializeObject<InterfaceEnrichmentResponse>(llmResponseJson);

                if (enrichment == null)
                {
                    result.AddSystemError("LLM response could not be deserialized into InterfaceEnrichmentResponse.");
                    return null;
                }

                return enrichment;
            }
            catch (Exception ex)
            {
                result.AddSystemError($"Failed to parse LLM enrichment response: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Applies the enrichment response to the InterfaceDescription, enforcing
        /// the length and style constraints from IDX-063.
        /// </summary>
        private void ApplyEnrichment(
            InterfaceDescription description,
            InterfaceEnrichmentResponse enrichment)
        {
            if (enrichment == null)
            {
                return;
            }

            description.OverviewSummary = Truncate(enrichment.OverviewSummary, OverviewSummaryMaxLength);

            description.Responsibilities = NormalizeList(
                enrichment.Responsibilities,
                ResponsibilitiesEntryMaxLength);

            description.UsageNotes = NormalizeList(
                enrichment.UsageNotes,
                UsageNotesEntryMaxLength);

            description.LinkageSummary = Truncate(enrichment.LinkageSummary, LinkageSummaryMaxLength);

            if (description.Methods != null && enrichment.Methods != null)
            {
                var methodLookup = enrichment.Methods;
                foreach (var method in description.Methods)
                {
                    if (methodLookup.TryGetValue(method.Name, out var semanticSummary))
                    {
                        method.SemanticSummary = Truncate(semanticSummary, MethodSummaryMaxLength);
                    }
                }
            }
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength
                ? trimmed
                : trimmed.Substring(0, maxLength);
        }

        private static IReadOnlyList<string> NormalizeList(
            IEnumerable<string> values,
            int entryMaxLength)
        {
            if (values == null)
            {
                return Array.Empty<string>();
            }

            var list = new List<string>();

            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var normalized = Truncate(value, entryMaxLength);
                list.Add(normalized);
            }

            return list;
        }

        /// <summary>
        /// Strongly typed representation of the expected LLM JSON response.
        /// </summary>
        private class InterfaceEnrichmentResponse
        {
            [JsonProperty("overviewSummary")]
            public string OverviewSummary { get; set; }

            [JsonProperty("responsibilities")]
            public List<string> Responsibilities { get; set; }

            [JsonProperty("usageNotes")]
            public List<string> UsageNotes { get; set; }

            [JsonProperty("linkageSummary")]
            public string LinkageSummary { get; set; }

            /// <summary>
            /// Map of method name to semantic summary string.
            /// </summary>
            [JsonProperty("methods")]
            public Dictionary<string, string> Methods { get; set; }
        }
    }
}
