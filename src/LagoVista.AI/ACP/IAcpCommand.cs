using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.Core.Validation;

namespace LagoVista.AI.ACP
{
    public interface IAcpCommand
    {
        Task<InvokeResult<IAgentPipelineContext>> ExecuteAsync(IAgentPipelineContext payload, string[] args);
    }
}
