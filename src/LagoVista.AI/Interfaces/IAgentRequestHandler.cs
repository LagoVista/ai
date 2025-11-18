using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Normalizes inbound client requests (browser, CLI, thick client)
    /// into core execution models and invokes the orchestrator.
    ///
    /// This is the seam between controllers and the agent orchestration
    /// pipeline. It is responsible for light validation, mapping into
    /// NewAgentExecutionSession or AgentExecutionRequest, and returning
    /// the AgentExecutionResponse produced by the orchestrator.
    /// </summary>
    public interface IAgentRequestHandler
    {
        Task<InvokeResult<AgentExecutionResponse>> HandleAsync(AgentRequestEnvelope request, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default);
    }
}
