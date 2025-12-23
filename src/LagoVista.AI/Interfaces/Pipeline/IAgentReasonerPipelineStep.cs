using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces.Pipeline
{
    public interface IAgentReasonerPipelineStep : IAgentPipelineStep
    {
        new Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx);
    }
}
