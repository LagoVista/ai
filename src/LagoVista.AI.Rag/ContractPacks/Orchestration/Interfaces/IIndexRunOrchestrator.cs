using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.ContractPacks.Orchestration.Interfaces
{
    /// <summary>
    /// High-level orchestrator for an entire indexing run.
    /// Responsible for:
    ///  - Loading configuration
    ///  - Discovering files
    ///  - Computing the ingestion plan
    ///  - Invoking the indexing pipeline for each file
    ///  - Invoking deletion logic for missing files
    ///
    /// It MUST NOT:
    ///  - Perform chunking, embedding, or direct vector database calls.
    ///    Those are delegated to IIndexingPipeline.
    /// </summary>
    public interface IIndexRunOrchestrator
    {
        /// <summary>
        /// Execute an indexing run for a given repo id.
        /// </summary>
        Task RunAsync(string repoId, CancellationToken token = default);
    }
}
