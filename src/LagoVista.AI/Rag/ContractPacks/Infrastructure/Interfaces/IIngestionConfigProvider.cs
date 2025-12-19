using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Rag.ContractPacks.Infrastructure.Interfaces
{
    /// <summary>
    /// Provides the ingestion configuration for an indexing run.
    /// Implementations typically load from appsettings, environment variables,
    /// or another configuration source.
    /// </summary>
    public interface IIngestionConfigProvider
    {
        /// <summary>
        /// Load the ingestion configuration.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The ingestion configuration.</returns>
        Task<InvokeResult<IngestionConfig>> LoadAsync(string json, CancellationToken cancellationToken = default);
    }
}
