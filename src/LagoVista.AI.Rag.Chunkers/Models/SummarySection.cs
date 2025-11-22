using System.Collections.Generic;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// Bridge between structured models and normalized, human-readable
    /// text for embedding into the vector store.
    /// </summary>
    public sealed class SummarySection
    {
        public string SectionKey { get; set; }
        public string Symbol { get; set; }
        public string SymbolType { get; set; }
        public string SectionNormalizedText { get; set; }
    }

    /// <summary>
    /// Contract implemented by structured description models that know
    /// how to project themselves into SummarySection instances.
    /// </summary>
    public interface ISummarySectionBuilder
    {
        IEnumerable<SummarySection> BuildSections();
    }
}
