using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces
{
    /// <summary>
    /// Responsibility: given a SourceRoot and repo definition, discover which
    /// files exist and provide basic information (full path, relative path, size,
    /// whether they are binary, etc.). Does not look at the local index.
    /// </summary>
    public interface IFileDiscoveryService
    {
        Task<IReadOnlyList<DiscoveredFile>> DiscoverAsync(IngestionConfig config, string repoId, CancellationToken token = default);
    }
    
}
