using LagoVista.AI.Models;
using LagoVista.AI.Rag.Models;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.ContractPacks.Orchestration.Interfaces
{
    /// <summary>
    /// High-level entry point for running an indexing pass.
    /// </summary>
    public interface IIndexRunOrchestrator
    {
        /// <summary>
        /// Execute an indexing run using the configured repositories and settings.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RunAsync(IngestionConfig config, string mode = "", string repoid = "", bool verbose = false, bool dryrun = false,  CancellationToken cancellationToken = default);
    }
}
