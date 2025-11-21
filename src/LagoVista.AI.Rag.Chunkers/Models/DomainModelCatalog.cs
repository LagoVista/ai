using System.Collections.Generic;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// In-memory catalog of domains and models used to enrich
    /// normalized text before embedding.
    /// </summary>
    public sealed class DomainModelCatalog
    {
        /// <summary>
        /// All known domains (titles, descriptions, keys, etc.).
        /// </summary>
        public IReadOnlyList<DomainSummaryInfo> Domains { get; set; }

        /// <summary>
        /// All known models with their associated domains and taglines.
        /// </summary>
        public IReadOnlyList<ModelSummaryInfo> Models { get; set; }
    }

    /// <summary>
    /// Lightweight summary for a model, suitable for enriching
    /// normalized text passed to the embedder.
    /// </summary>
    public sealed class ModelSummaryInfo
    {
        /// <summary>
        /// Domain key this model belongs to (e.g. "AI Admin").
        /// </summary>
        public string DomainKey { get; set; }

        /// <summary>
        /// Underlying model name / symbol (e.g. "Device").
        /// </summary>
        public string ModelName { get; set; }

        /// <summary>
        /// Human-facing title for the model.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Short tagline or one-line description for the model.
        /// </summary>
        public string Tagline { get; set; }
    }
}
