using LagoVista.AI.Chunkers.Providers.DomainDescription;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Rag.Chunkers.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.Interfaces
{
    /// <summary>
    /// Contract for a single indexing pipeline that can index one file at a time.
    /// Implementations are responsible for:
    ///  - Chunking
    ///  - Embedding
    ///  - Sending vectors to the backing store (e.g., Qdrant)
    ///  - Emitting any metadata needed for registries
    ///
    /// They should NOT:
    ///  - Walk the file system
    ///  - Plan which files to index
    ///  - Manage local index persistence
    /// </summary>
    public interface IIndexingPipeline
    {
        /// <summary>
        /// Index a single file identified by the context.
        /// </summary>
        Task IndexFileAsync(DomainModelCatalog domainCatalog, Dictionary<string, string> resourceDictionary, IndexFileContext context, CancellationToken token = default);
    }
}
