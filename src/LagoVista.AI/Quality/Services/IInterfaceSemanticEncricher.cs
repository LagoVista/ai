using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Models;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Quality.Services
{
    public interface IInterfaceSemanticEnricher
    {
        /// <summary>
        /// Enrich the given InterfaceDescription according to IDX-063 using an LLM.
        /// </summary>
        /// <param name="description">Pre-populated interface description (structural fields).</param>
        /// <param name="agentContext">AgentContext containing LLM configuration (endpoint, keys, model, etc.).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>InvokeResult with the enriched InterfaceDescription.</returns>
        Task<InvokeResult<InterfaceDescription>> EnrichAsync(
            InterfaceDescription description,
            IngestionConfig config,
            CancellationToken cancellationToken = default);
    }
}
