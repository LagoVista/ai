using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces.Pipeline
{
    public interface IAgentContextResolverPipelineStep : IAgentPipelineStep
    {
        new Task<InvokeResult<IAgentPipelineContext>> ExecuteAsync(IAgentPipelineContext ctx);
    }
}
