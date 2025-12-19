using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.Interfaces
{
    /// <summary>
    /// LLM-backed review service for refining titles, descriptions, and optional help text
    /// for models and domains according to IDX-066.
    /// </summary>
    public interface ITitleDescriptionReviewService
    {
        /// <summary>
        /// Review and optionally refine the supplied metadata using an LLM.
        /// The service must obey the safety and guardrail rules defined in IDX-066.
        /// </summary>
        /// <param name="kind">Model or Domain.</param>
        /// <param name="symbolName">Class name (model) or domain symbol.</param>
        /// <param name="title">Original title text.</param>
        /// <param name="description">Original description text.</param>
        /// <param name="help">Optional help text (may be null).</param>
        /// <param name="contextBlob">
        /// Additional blended context provided by the orchestrator (for example,
        /// key fields or domain entity summaries). The service is responsible for
        /// merging this into the payload sent to the LLM.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<TitleDescriptionReviewResult> ReviewAsync(
             SummaryObjectKind kind,
             string symbolName,
             string title,
             string description,
             string help,
             string contextBlob,
             CancellationToken cancellationToken = default);
    }
}
