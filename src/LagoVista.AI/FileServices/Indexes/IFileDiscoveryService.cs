using LagoVista.AI.Indexing.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.FileServices.Indexes
{
    /// <summary>
    /// Responsibility: given a SourceRoot and repo definition, discover which
    /// files exist and provide basic information (full path, relative path, size,
    /// whether they are binary, etc.). Does not look at the local index.
    /// </summary>
    public interface IFileDiscoveryService
    {
        Task<IReadOnlyList<DiscoveredFile>> DiscoverAsync(IngestionConfig config, string repoId, string extension = "", CancellationToken token = default);
        Task<IReadOnlyList<DiscoveredFile>> DiscoverAsync(IngestionConfig config, string extension = "", CancellationToken token = default);
    }

}
