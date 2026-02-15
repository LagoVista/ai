using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Indexing.Models;

namespace LagoVista.AI.Quality.Interfaces
{
    /// <summary>
    /// Service responsible for writing refined title/description values back to
    /// DomainDescriptor C# files for domain metadata.
    /// </summary>
    public interface IDomainDescriptorUpdateService
    {
        /// <summary>
        /// Update the DomainDescription initializer for the specified domain using
        /// the refined values from <paramref name="review"/>.
        /// </summary>
        /// <param name="domain">Domain metadata produced by the domain scanner.</param>
        /// <param name="review">LLM refinement result.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UpdateAsync(
            DomainMetadata domain,
            TitleDescriptionReviewResult review,
            CancellationToken cancellationToken);
    }
}
