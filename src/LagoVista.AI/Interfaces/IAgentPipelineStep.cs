using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;
using System.Threading;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentPipelineStep
    {
        Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(
            AgentPipelineContext ctx,
            CancellationToken cancellationToken = default);
    }

}
