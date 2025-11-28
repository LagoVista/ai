using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;
using System.Threading;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Server-side tool that can be invoked by the Aptix reasoner.
    /// 
    /// The tool is identified by a stable Name that must match the
    /// LLM tool name (and what you register in AgentToolRegistry).
    /// </summary>
    public interface IAgentTool
    {
        /// <summary>
        /// Stable logical name of this tool. Must match the tool name
        /// used in the LLM "tools" parameter and registry registration.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Execute the tool using the raw JSON arguments emitted by the LLM
        /// and a rich execution context (agent, conversation, org, user, etc.).
        /// 
        /// Returns a JSON string payload that will be fed back to the LLM
        /// as the tool result.
        /// </summary>
        Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default);
    }
}
