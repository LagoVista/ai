using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentTurnExecutor
    {
        Task<InvokeResult<AgentExecutionResponse>> ExecuteNewSessionTurnAsync(AgentSession session, AgentSessionTurn turn, NewAgentExecutionSession request, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default);

        Task<InvokeResult<AgentExecutionResponse>> ExecuteFollowupTurnAsync(AgentSession session, AgentSessionTurn turn, AgentExecutionRequest request, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default);
    }
}
