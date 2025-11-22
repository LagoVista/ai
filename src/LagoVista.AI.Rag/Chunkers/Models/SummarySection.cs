using System.Collections.Generic;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// Small, stable bridge between structured description models and
    /// normalized text that will be chunked and embedded.
    /// </summary>
    public sealed class SummarySection
    {
        /// <summary>
        /// Logical identifier for the section, e.g. "model-overview",
        /// "manager-methods", "repository-queries".
        /// </summary>
        public string SectionKey { get; set; }

        /// <summary>
        /// The symbol this section describes (class, interface, controller, etc.).
        /// Typically a simple type name such as "Device" or "DeviceManager".
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// High-level classification for the symbol, e.g. "Model", "Manager",
        /// "Repository", "Interface", "Controller".
        /// </summary>
        public string SymbolType { get; set; }

        /// <summary>
        /// Human-readable, deterministic text representation that will be used as
        /// the basis for RagChunk.TextNormalized and embedding.
        /// </summary>
        public string SectionNormalizedText { get; set; }
    }

    /// <summary>
    /// Implemented by structured ChunkFlavor types (or their adapters) to
    /// convert rich description models into one or more SummarySection
    /// instances.
    /// </summary>
    public interface ISummarySectionBuilder
    {
        /// <summary>
        /// Build one or more SummarySection objects that describe the
        /// underlying symbol in human-readable form.
        /// </summary>
        IEnumerable<SummarySection> BuildSections();
    }
}
