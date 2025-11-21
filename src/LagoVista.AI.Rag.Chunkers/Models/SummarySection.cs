// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: TBD
// IndexVersion: 1
// --- END CODE INDEX META ---
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// Generic, human-readable summary slice for any ChunkFlavor (Model, Manager, etc.).
    /// This is what we actually normalize and embed.
    /// </summary>
    public sealed class SummarySection
    {
        /// <summary>
        /// Stable key for this section within a symbol, e.g. "model-overview", "model-properties".
        /// Used as RagChunk.SectionKey and for payload filtering.
        /// </summary>
        public string SectionKey { get; set; }

        /// <summary>
        /// High-level classification for the section, e.g. "Overview", "Properties", "Relationships".
        /// Primarily for diagnostics / future filtering; we also bake this into SectionNormalizedText.
        /// </summary>
        public string SectionType { get; set; }

        /// <summary>
        /// Logical symbol this section is about, e.g. model/class name "Device".
        /// Maps to RagVectorPayload.Symbol.
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// Kind of symbol, e.g. "Model", "Manager", "Repository", "Controller".
        /// Maps to RagVectorPayload.SymbolType.
        /// </summary>
        public string SymbolType { get; set; }

        /// <summary>
        /// Human-facing title for this section, e.g. "Device Model - Properties".
        /// Usually included as a heading inside SectionNormalizedText as well.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Fully formatted, human-readable text that will be fed to the embedder.
        /// May later be split into one or more RagChunks, but each chunk keeps this
        /// section's Symbol / SymbolType / SectionKey in its payload.
        /// </summary>
        public string SectionNormalizedText { get; set; }
    }

   
}
