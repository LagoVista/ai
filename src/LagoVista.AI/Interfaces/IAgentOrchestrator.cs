using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
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
        Task<InvokeResult<AgentExecuteResponse>> BeginNewSessionAsync(AgentExecuteRequest request, EntityHeader org,
            EntityHeader user, CancellationToken cancellationToken = default);
    
        Task<InvokeResult<AgentExecuteResponse>> ExecuteTurnAsync(AgentExecuteRequest request, EntityHeader org,
            EntityHeader user, CancellationToken cancellationToken = default);
    }
}
