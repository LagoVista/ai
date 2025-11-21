using LagoVista.AI.Rag.Chunkers.Models;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Chunkers.Interfaces
{
    /// <summary>
    /// Contract implemented by any ChunkFlavor that knows how to present itself
    /// as one or more SummarySection instances ready for token-budgeted chunking
    /// and embedding.
    /// </summary>
    public interface ISummarySectionBuilder
    {
        /// <summary>
        /// Build the logical summary sections for this flavor instance.
        /// Each section should be self-contained, human-readable text that the
        /// embedding pipeline can further split if needed.
        /// </summary>
        IEnumerable<SummarySection> BuildSummarySections();
    }
}
