using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.FileServices.Indexes
{
    /// <summary>
    /// Responsibility: for a single planned file, build the <see cref="IndexFileContext"/>
    /// by computing full path, content hash, and canonical <see cref="DocumentIdentity"/>,
    /// and updating the corresponding <see cref="LocalIndexRecord"/> in the local index.
    /// </summary>
    public interface IIndexFileContextBuilder
    {
        /// <summary>
        /// Build the indexing context for a single file.
        /// </summary>
        /// <param name="config">Global ingestion configuration.</param>
        /// <param name="repoId">Logical repository identifier.</param>
        /// <param name="plannedFile">Planned ingestion decision for this file.</param>
        /// <param name="localIndex">Local index store for this repository.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Indexing context for the file.</returns>
        Task<IndexFileContext> BuildAsync(
            IngestionConfig config,
            GitRepoInfo gitRepoInfo,
            string repoId,
            PlannedFileIngestion plannedFile,
            LocalIndexStore localIndex,
            CancellationToken token = default);
    }
}
