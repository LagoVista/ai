using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Rag.ContractPacks.Quality.Interfaces
{
    /// <summary>
    /// Abstraction over the LLM-backed quality check for titles and descriptions.
    /// This lives in the Rag project so the ingestion pipeline can depend on it
    /// without referencing a concrete implementation.
    /// </summary>
    public interface ITitleDescriptionReviewService
    {
        Task<TitleDescriptionReviewResult> ReviewAsync(
            SummaryObjectKind kind,
            string symbolName,
            string title,
            string description,
            CancellationToken cancellationToken = default);
    }
}
