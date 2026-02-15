using System.Collections.Generic;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Indexing.Models
{
    /// <summary>
    /// Canonical description contract for IDX-069. Implementations are built by description
    /// builders and are responsible for deterministic SummarySection and RAG point creation.
    /// </summary>
    public interface IRagDescription
    {
        /// <summary>
        /// Builds the atomic SummarySection units used for indexing.
        /// Implementations must be deterministic and must not perform I/O or LLM calls.
        /// </summary>
        IEnumerable<SummarySection> BuildSummarySections();

        /// <summary>
        /// Builds RAG points from the underlying SummarySections using invariant rules.
        /// Implementations must be deterministic and must not perform I/O or LLM calls.
        /// </summary>
        IEnumerable<InvokeResult<IRagPoint>> BuildRagPoints();
    }
}
