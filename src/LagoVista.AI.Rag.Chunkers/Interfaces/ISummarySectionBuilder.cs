using LagoVista.AI.Rag.Chunkers.Models;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Chunkers.Interfaces
{
    /// <summary>
    /// Contract implemented by any ChunkFlavor that knows how to present itself
    /// as one or more SummarySection instances ready for token-budgeted chunking
    /// and embedding.
    /// </summary>

    /// <summary>
    /// Contract implemented by structured description models that know
    /// how to project themselves into SummarySection instances.
    /// </summary>
    public interface ISummarySectionBuilder
    {
        IEnumerable<SummarySection> BuildSections(DomainModelHeaderInformation headerInfo, int maxTokens = 6500);
    }
}
