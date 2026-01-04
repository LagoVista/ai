using System.Collections.Generic;
using Newtonsoft.Json;

namespace LagoVista.AI.Rag.Models
{
    /// <summary>
    /// The full payload sent to the ITitleDescriptionLlmClient for structured
    /// refinement of model or domain title / description / help text.
    ///
    /// This is the canonical request for IDX-066 and represents everything
    /// the LLM should know about the entity we are refining.
    /// </summary>
    public class TitleDescriptionReviewRequest
    {
        /// <summary>
        /// Whether this request represents a Model or a Domain.
        /// </summary>
        [JsonProperty("kind")]
        public SummaryObjectKind Kind { get; set; }

        /// <summary>
        /// Kind of the class (for models) or constant identifier (for domains).
        /// </summary>
        [JsonProperty("symbolName")]
        public string SymbolName { get; set; }

        /// <summary>
        /// Current human-facing title text.
        /// </summary>
        [JsonProperty("title")]
        public string Title { get; set; }

        /// <summary>
        /// Current human-facing description text.
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>
        /// Optional help text associated with the model or domain.
        /// Null = no help currently exists.
        /// </summary>
        [JsonProperty("help")]
        public string Help { get; set; }

        /// <summary>
        /// Optional: name of the model used for refinement (defaults to gpt-5.1).
        /// </summary>
        [JsonProperty("model")]
        public string Model { get; set; }

        // --------------------------------------------------------------------
        // Domain Context (only meaningful for Model refinement)
        // --------------------------------------------------------------------

        /// <summary>
        /// Domain key from the AIDomain class (e.g. AIDomain.AIAdmin).
        /// </summary>
        [JsonProperty("domainKey")]
        public string DomainKey { get; set; }

        /// <summary>
        /// Domain name as defined in the DomainDescriptor.
        /// </summary>
        [JsonProperty("domainName")]
        public string DomainName { get; set; }

        /// <summary>
        /// Domain description text as defined in the DomainDescriptor.
        /// </summary>
        [JsonProperty("domainDescription")]
        public string DomainDescription { get; set; }

        // --------------------------------------------------------------------
        // Model Field Metadata (only used for SummaryObjectKind.Model)
        // --------------------------------------------------------------------

        /// <summary>
        /// Additional model context extracted from FormFields.
        /// These help the LLM understand what the entity represents.
        /// </summary>
        [JsonProperty("fields")]
        public List<ModelFieldMetadata> Fields { get; set; } = new List<ModelFieldMetadata>();

        // --------------------------------------------------------------------
        // Blended context blob
        // --------------------------------------------------------------------

        /// <summary>
        /// JSON blob that blends together all available context (kind, symbol,
        /// title/description/help, domain metadata, field metadata, and any
        /// additional orchestrator-provided hints).
        ///
        /// The HttpLlmTitleDescriptionClient passes this verbatim to the LLM
        /// as part of the user payload. Keeping this as a single blob gives us
        /// flexibility to evolve the shape without changing the wire contract.
        /// </summary>
        [JsonProperty("contextBlob")]
        public string ContextBlob { get; set; }

        /// <summary>
        /// Helper: pretty-print everything as JSON for debugging.
        /// </summary>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
