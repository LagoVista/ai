using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Retrieves DDR consumption fields used for prompt construction.
    ///
    /// Per AGN-035:
    /// - Instruction DDRs: DetailDesignReview.AgentInstruction
    /// - Reference DDRs: DetailDesignReview.ReferentialSummary
    ///
    /// This provider should be cache-backed and support batch retrieval.
    /// </summary>
    public interface IDdrConsumptionFieldProvider
    {
        Task<InvokeResult<IDictionary<string, DdrModelFields>>> GetDdrModelSummaryAsync(
            string orgId,
            IEnumerable<string> ddrIds,
            CancellationToken cancellationToken = default);
    }
}
