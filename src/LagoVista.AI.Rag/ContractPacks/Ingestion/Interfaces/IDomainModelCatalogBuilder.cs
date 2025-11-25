using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces
{
    /// <summary>
    /// Responsibility: given a set of discovered files for a repository,
    /// use Roslyn/RAG chunker services to build an in-memory catalog of
    /// domains and models.
    /// </summary>
    public interface IDomainModelCatalogBuilder
    {
        /// <summary>
        /// Build the Domain/Model catalog for a single repository.
        /// </summary>
        /// <param name="repoId">Repository identifier (must match discovery step).</param>
        /// <param name="files">Discovered files from the repository.</param>
        /// <param name="token">Cancellation token.</param>
        Task<DomainModelCatalog> BuildAsync(string repoId, IReadOnlyList<DiscoveredFile> files, IReadOnlyDictionary<string, string> resources, CancellationToken token = default);
    }
}
