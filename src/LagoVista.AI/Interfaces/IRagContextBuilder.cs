using System.Collections.Generic;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Builds an AGN-002 compliant RAG context block from vector search results.
    /// </summary>
    public interface IRagContextBuilder
    {
        /// <summary>
        /// Embeds the provided instructions, performs a vector search using the configured
        /// vector database, applies the provided RAG scope filters, and returns a single
        /// AGN-002 formatted [CONTEXT] block as the string payload.
        /// </summary>
        /// <param name="instructions">Instructions / query text to embed and search on.</param>
        /// <param name="ragScopeFilters">Key/value filters applied to the vector search payload.</param>
        Task<InvokeResult<string>> BuildContextSectionAsync(AgentContext agentContext, string instructions, RagScopeFilter scopeFilter);
    }
}
