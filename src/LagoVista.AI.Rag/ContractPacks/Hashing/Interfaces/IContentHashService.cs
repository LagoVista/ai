using System.Threading.Tasks;

namespace LagoVista.AI.Rag.ContractPacks.Hashing.Interfaces
{
    /// <summary>
    /// Contract for computing normalized content hashes used by the indexing
    /// system. Implementations should delegate to the canonical ContentHashHelper
    /// or equivalent logic.
    /// </summary>
    public interface IContentHashService
    {
        /// <summary>
        /// Compute a normalized hash for the contents of a file at the given path.
        /// </summary>
        Task<string> ComputeFileHashAsync(string fullPath);

        /// <summary>
        /// Compute a normalized hash for an in-memory text buffer.
        /// </summary>
        string ComputeTextHash(string content);
    }
}
