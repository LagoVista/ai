using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Helpers;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Services;
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Indexing
{
    public sealed class EntityIndexMeta
    {
        public string Id { get; set; }
        public string DatabaseName { get; set; }
        public string EntityType { get; set; }
        public string Name { get; set; }
        public string Key { get; set; }

        public bool IsPublic { get; set; }
        public EntityHeader OwnerOrganization { get; set; }
        public EntityHeader OwnerUser { get; set; }

        public EntityHeader Category { get; set; }
        public string Description { get; set; }

        public bool? IsDeleted { get; set; }
        public bool IsDeprecated { get; set; }
        public bool IsDraft { get; set; }

        public string CreationDate { get; set; }
        public string LastUpdatedDate { get; set; }

        public int Revision { get; set; }
        public string RevisionTimeStamp { get; set; }

        public List<LagoVista.Core.Models.Label> Labels { get; set; } = new List<LagoVista.Core.Models.Label>();
        public double? Stars { get; set; }
        public int RatingsCount { get; set; }
    }

    public sealed class EntityIndexLenses
    {
        public string EmbedSnippet { get; set; }
        public string ModelSummary { get; set; }
        public string UserDetail { get; set; }
        public string CleanupGuidance { get; set; }
    }

    public sealed class EntityIndexDocument
    {
        public EntityIndexMeta Meta { get; set; }
        public EntityIndexLenses Lenses { get; set; }

        // Optional: keep for debugging or re-indexing without reloading source
        public JObject DomainPayload { get; set; }
    }

    public interface IIndexLensModelClient
    {
        /// <summary>
        /// Takes a prompt and returns the model's raw string response.
        /// Response MUST be a JSON object with keys embedSnippet, modelSummary, userDetail, cleanupGuidance.
        /// </summary>
        Task<string> CompleteAsync(string prompt, CancellationToken ct);
    }

    public sealed class EntityIndexDocumentBuilder : IEntityIndexDocumentBuilder
    {
        public const string IndexLensInstructions = @"
You are generating normalized text outputs for indexing and retrieval.

You will receive TWO inputs:

1) EntityBaseHeader
- This contains authoritative metadata (id, entityType, name, key, ownership, lifecycle, revision).
- These fields are already standardized and MUST NOT be re-derived, reformatted, or guessed.

2) DomainPayload
- This contains ONLY domain-specific fields.
- EntityBase fields have already been removed.
- HTML has already been stripped and normalized to plain text.

Your task is to generate FOUR text lenses from the DomainPayload, using the EntityBaseHeader only for light contextual grounding (e.g., entityType, name).

--------------------------------
LENS DEFINITIONS
--------------------------------

1) embedSnippet
Purpose:
- Used for vector embeddings and similarity search.

Rules:
- Keyword-dense, information-rich, minimal prose.
- Prefer short bullet-like phrases.
- Include role, audience, problem space, core capabilities, outcomes, and integrations when present.
- Avoid marketing fluff, filler sentences, or formatting.
- Do NOT include IDs, timestamps, or audit details.

Target length:
- ~400–1200 characters.

2) modelSummary
Purpose:
- Help a language model understand what this record represents and how to reason about it.

Rules:
- Clear, factual, explanatory tone.
- Answer implicitly: what this is, why it matters, and when to use it.
- Reference the entity type and purpose.
- Do NOT invent missing details.
- Do NOT restate EntityBase metadata verbatim.

Target length:
- 120–250 words.

3) userDetail
Purpose:
- Human-readable detail suitable for UI display or end-user explanation.

Rules:
- Structured, scannable sections (headings and bullets).
- Emphasize responsibilities, problems, solution, benefits, and use cases as applicable.
- Convert domain concepts into clear explanations.
- Do NOT include raw metadata or system fields.

Target length:
- 300–900 words.

4) cleanupGuidance
Purpose:
- Suggest how this record could be improved or normalized over time.

Rules:
- DO NOT invent values.
- Only identify:
  - Missing but important fields
  - Inconsistent formats
  - Ambiguities that affect understanding or retrieval
  - Safe normalization or enrichment opportunities
- Group bullets under categories such as:
  - Missing information
  - Normalization improvements
  - Search/retrieval enrichment
  - Safe auto-fixes

Target length:
- 8–20 concise bullets total.

--------------------------------
GLOBAL RULES
--------------------------------

- If information is missing or unclear, omit it (except in cleanupGuidance).
- Do NOT hallucinate specifications, capabilities, or integrations.
- Do NOT repeat the input JSON or describe the inputs themselves.
- Do NOT use markdown or code fences.

--------------------------------
OUTPUT FORMAT (STRICT)
--------------------------------

Return ONLY valid JSON with EXACTLY these keys:

{
  ""embedSnippet"": ""..."",
  ""modelSummary"": ""..."",
  ""userDetail"": ""..."",
  ""cleanupGuidance"": ""...""
}

No additional keys.
No surrounding text.
No explanations outside the JSON.
";


        private static readonly HashSet<string> EntityBasePropertyNames =
            typeof(EntityBase)
                .GetRuntimeProperties()
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        private readonly IStructuredTextLlmService _modelClient;

        public EntityIndexDocumentBuilder(IStructuredTextLlmService modelClient)
        {
            _modelClient = modelClient ?? throw new ArgumentNullException(nameof(modelClient));
        }

        private static void NormalizeHtmlFields(JToken token)
        {
            if (token is JProperty prop && prop.Value.Type == JTokenType.String)
            {
                var value = prop.Value.Value<string>();
                if (LooksLikeHtml(value))
                {
                    prop.Value = HtmlTextNormalizer.ToPlainText(value);
                }
            }

            if (token is JContainer container)
            {
                foreach (var child in container.Children())
                    NormalizeHtmlFields(child);
            }
        }

        private static bool LooksLikeHtml(string value)
        {
            return value != null &&
                   value.IndexOf('<') >= 0 &&
                   value.IndexOf('>') >= 0;
        }


        public async Task<InvokeResult<EntityIndexDocument>> BuildAsync(IEntityBase entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var meta = ExtractMeta(entity);
            var domainPayload = ExtractDomainPayload(entity);

            var prompt = BuildPrompt(meta, domainPayload);

            var raw = await _modelClient.ExecuteAsync<EntityIndexLenses>(IndexLensInstructions, prompt);
            if (raw.Successful)
            {
                var lenses = raw.Result;

                return InvokeResult<EntityIndexDocument>.Create(new EntityIndexDocument
                {
                    Meta = meta,
                    Lenses = lenses,
                    DomainPayload = domainPayload
                });
            }

            return InvokeResult<EntityIndexDocument>.FromInvokeResult(raw.ToInvokeResult());
        }

        private static EntityIndexMeta ExtractMeta(IEntityBase e) => new EntityIndexMeta
        {
            Id = e.Id,
            DatabaseName = e.DatabaseName,
            EntityType = e.EntityType,
            Name = e.Name,
            Key = e.Key,

            IsPublic = e.IsPublic,
            OwnerOrganization = e.OwnerOrganization,
            OwnerUser = e.OwnerUser,

            Category = e.Category,
            Description = e.Description,

            IsDeprecated = e.IsDeprecated,
            IsDraft = e.IsDraft,

            CreationDate = e.CreationDate,
            LastUpdatedDate = e.LastUpdatedDate,

            Revision = e.Revision,
            RevisionTimeStamp = e.RevisionTimeStamp,

            Labels = e.Labels ?? new List<LagoVista.Core.Models.Label>(),
            Stars = e.Stars,
            RatingsCount = e.RatingsCount
        };

        private static JObject ExtractDomainPayload(IEntityBase entity)
        {
            var json = JsonConvert.SerializeObject(entity, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            var obj = JObject.Parse(json);

            // Remove anything that is part of EntityBase to keep the domain payload clean.
            foreach (var baseProp in EntityBasePropertyNames)
            {
                // JSON property names usually match CLR names except Id which is [JsonProperty("id")]
                // Remove both "Id" and "id" patterns safely.
                obj.Remove(baseProp);
                obj.Remove(ToLowerCamel(baseProp));

                if (string.Equals(baseProp, nameof(EntityBase.Id), StringComparison.OrdinalIgnoreCase))
                {
                    obj.Remove("id"); // explicit because of [JsonProperty("id")]
                }
            }

            NormalizeHtmlFields(obj);

            // You may also want to strip high-noise sections from derived types consistently.
            // Example: auditHistory tends to be huge and not helpful for lens generation.
            obj.Remove("auditHistory");

            return obj;
        }

        private string BuildPrompt(EntityIndexMeta meta, JObject domainPayload)
        {
            var headerJson = JsonConvert.SerializeObject(meta, Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            var domainJson = domainPayload.ToString(Formatting.Indented);

            return
$@"EntityBaseHeader:
{headerJson}

DomainPayload:
{domainJson}
";
        }

        private static EntityIndexLenses ParseLenses(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new InvalidOperationException("Model returned an empty response.");

            JObject obj;
            try
            {
                obj = JObject.Parse(raw);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Model response was not valid JSON. Raw: {Truncate(raw, 800)}", ex);
            }

            string GetRequired(string name)
            {
                var token = obj[name];
                var value = token?.Type == JTokenType.String ? token.Value<string>() : null;
                if (string.IsNullOrWhiteSpace(value))
                    throw new InvalidOperationException($"Model JSON missing required string field '{name}'.");
                return value.Trim();
            }

            return new EntityIndexLenses
            {
                EmbedSnippet = GetRequired("embedSnippet"),
                ModelSummary = GetRequired("modelSummary"),
                UserDetail = GetRequired("userDetail"),
                CleanupGuidance = GetRequired("cleanupGuidance")
            };
        }

        private static string ToLowerCamel(string s)
        {
            if (string.IsNullOrWhiteSpace(s) || !char.IsUpper(s[0])) return s;
            return char.ToLowerInvariant(s[0]) + s[1..];
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "...";
        }
    }
}
