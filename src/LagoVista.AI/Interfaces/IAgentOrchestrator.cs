using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Coordinates the full Aptix agent execution flow for a single request,
    /// including session/turn management, RAG, and OpenAI calls.
    /// </summary>
    public interface IAgentOrchestrator
    {
        Task<InvokeResult<AgentExecutionResponse>> ExecuteAsync(
            AgentExecutionRequest request,
            EntityHeader org,
            EntityHeader user,
            CancellationToken cancellationToken = default);
    }
}
