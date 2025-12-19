using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.ContractPacks.Registry.Interfaces
{
    /// <summary>
    /// Reports aggregated facet values discovered during an indexing run.
    /// This is global, repo-level metadata (NOT per-document data).
    /// </summary>
    public interface IMetadataRegistryClient
    {
        /// <summary>
        /// Report all unique facet values discovered during an indexing run.
        /// The implementation is responsible for persisting these in the
        /// metadata registry for later use (UI filters, analytics, etc.).
        /// </summary>
        /// <param name="orgId">Organization identifier.</param>
        /// <param name="projectId">Project identifier.</param>
        /// <param name="repoId">Repository identifier.</param>
        /// <param name="facets">All unique facets discovered during the run.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ReportFacetsAsync(
            string orgId,
            string projectId,
            string repoId,
            IReadOnlyList<FacetValue> facets,
            CancellationToken cancellationToken = default);
    }
}
