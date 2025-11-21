using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// Abstraction over the LLM-backed quality check for titles and descriptions.
    /// </summary>
    public interface ITitleDescriptionReviewService
    {
        /// <summary>
        /// Ask an LLM to review and, if needed, suggest improvements to the title and description.
        /// </summary>
        Task<TitleDescriptionReviewResult> ReviewAsync(
            SummaryObjectKind kind,
            string symbolName,
            string title,
            string description,
            CancellationToken cancellationToken = default);
    }
}
