using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Executes server-side tools given a parsed AgentToolCall.
    /// 
    /// If the tool name is not registered as a server tool, the call
    /// is returned unchanged (IsServerTool = false, WasExecuted = false),
    /// allowing the client to pick it up.
    /// </summary>
    public interface IAgentToolExecutor
    {
        /// <summary>
        /// Attempt to execute the given tool call on the server.
        /// 
        /// - If the tool is not registered: returns the call unchanged.
        /// - If the tool is registered and executes successfully:
        ///     IsServerTool = true, WasExecuted = true, ResultJson set.
        /// - If the tool fails to execute:
        ///     IsServerTool = true, WasExecuted = false, ErrorMessage set.
        /// </summary>
        Task<InvokeResult<AgentToolCallResult>> ExecuteServerToolAsync(
            AgentToolCall call,
            AgentPipelineContext context);
    }
}
