using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Coordinates LLM calls and server-side tool execution for a single agent turn.
    ///
    /// Responsibilities:
    /// - Call the underlying ILLMClient with the current AgentExecuteRequest.
    /// - Inspect AgentExecuteResponse.ToolCalls for tool invocations.
    /// - Execute any server-side tools and feed their results back into the LLM.
    /// - Stop and return early if any non-server (client) tools are requested.
    /// </summary>
    public interface IAgentReasoner
    {
        /// <summary>
        /// Execute the reasoning loop for a single Aptix agent turn.
        ///
        /// May call the LLM multiple times if server-only tools are involved.
        /// If client tools are requested, returns once those tool calls are
        /// discovered, leaving client execution to the caller.
        /// </summary>
        Task<InvokeResult<AgentExecuteResponse>> ExecuteAsync(
            AgentContext agentContext,
            ConversationContext conversationContext,
            AgentExecuteRequest request,
            string ragContextBlock,
            string sessionId,
            EntityHeader org,
            EntityHeader user,
            CancellationToken cancellationToken = default);
    }
}
