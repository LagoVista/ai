using LagoVista.AI.Indexing.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.Interfaces
{
    /// <summary>
    /// Contract abstraction over the local index persistence mechanism.
    /// Existing LocalIndexStore implementation can be adapted to this
    /// interface. This interface lives in the Models namespace to avoid
    /// circular references with services.
    /// </summary>
    public interface ILocalIndexStore
    {
        /// <summary>
        /// Load the local index for the specified repo id.
        /// </summary>
        Task<LocalIndexStore> LoadAsync(IngestionConfig config, string repoId, CancellationToken token = default);

        /// <summary>
        /// Persist the updated local index for the specified repo id.
        /// </summary>
        Task SaveAsync(IngestionConfig config, string repoId, LocalIndexStore store, CancellationToken token = default);

        /// <summary>
        /// Enumerate all records in the local index.
        /// </summary>
        IReadOnlyList<LocalIndexRecord> GetAll(LocalIndexStore store);
    }
}
