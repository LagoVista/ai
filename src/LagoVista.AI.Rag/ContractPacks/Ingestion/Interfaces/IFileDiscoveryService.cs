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
        Task<IReadOnlyList<DiscoveredFile>> DiscoverAsync(string repoId, CancellationToken token = default);
    }

    /// <summary>
    /// Simple descriptor for a discovered file.
    /// </summary>
    public class DiscoveredFile
    {
        public string RepoId { get; set; }
        public string FullPath { get; set; }
        public string RelativePath { get; set; }
        public long SizeBytes { get; set; }
        public bool IsBinary { get; set; }
    }
}
