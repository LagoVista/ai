using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces.Pipeline
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
    public interface IAgentReasoner : IAgentPipelineStep
    {
    }
}
