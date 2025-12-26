using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        Task<InvokeResult<IDictionary<string, string>>> GetAgentInstructionsAsync(
            string orgId,
            IEnumerable<string> ddrIds,
            CancellationToken cancellationToken = default);

        Task<InvokeResult<IDictionary<string, string>>> GetReferentialSummariesAsync(
            string orgId,
            IEnumerable<string> ddrIds,
            CancellationToken cancellationToken = default);
    }
}
