using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Models
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
        Task<LocalIndexStore> LoadAsync(string repoId, CancellationToken token = default);

        /// <summary>
        /// Persist the updated local index for the specified repo id.
        /// </summary>
        Task SaveAsync(string repoId, LocalIndexStore store, CancellationToken token = default);

        /// <summary>
        /// Enumerate all records in the local index.
        /// </summary>
        IReadOnlyList<LocalIndexRecord> GetAll(LocalIndexStore store);
    }
}
