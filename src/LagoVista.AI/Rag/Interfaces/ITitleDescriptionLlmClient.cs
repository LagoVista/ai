using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.Interfaces
{
    /// <summary>
    /// Low-level LLM client used by the title/description refinement pipeline (IDX-066).
    ///
    /// This client is responsible ONLY for:
    /// - Building the /v1/responses request payload.
    /// - Applying the structured-output (json_schema) envelope.
    /// - Parsing the structured JSON payload back into a TitleDescriptionReviewResult.
    ///
    /// Guard rails ("keep original" semantics, catalog updates, etc.) live in
    /// ITitleDescriptionReviewService/TitleDescriptionRefinementOrchestrator.
    /// </summary>
    public interface ITitleDescriptionLlmClient
    {
        /// <summary>
        /// Send a single title/description (and optional help) review request to the LLM
        /// and return the structured review result.
        ///
        /// This method is intentionally low-level and expects the caller to apply
        /// higher-level guard rails and catalog updates.
        /// </summary>
        /// <param name="request">Fully-populated review request (model + domain + fields).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Structured review result returned by the LLM.</returns>
        Task<TitleDescriptionReviewResult> ReviewAsync(
            TitleDescriptionReviewRequest request,
            CancellationToken cancellationToken = default);
    }
}
