using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Executes a single logical turn for an Aptix agent session,
    /// given the already-loaded AgentContext.
    /// </summary>
    public interface IAgentTurnExecutor
    {
        Task<InvokeResult<AgentExecutionResponse>> ExecuteNewSessionTurnAsync(
            AgentContext agentContext,
            AgentSession session,
            AgentSessionTurn turn,
            NewAgentExecutionSession request,
            EntityHeader org,
            EntityHeader user,
            CancellationToken cancellationToken = default);

        Task<InvokeResult<AgentExecutionResponse>> ExecuteFollowupTurnAsync(
            AgentContext agentContext,
            AgentSession session,
            AgentSessionTurn turn,
            AgentExecutionRequest request,
            EntityHeader org,
            EntityHeader user,
            CancellationToken cancellationToken = default);
    }
}
