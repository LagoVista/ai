using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentExecuteResponseParser
    {
        Task<InvokeResult<IAgentPipelineContext>> ParseAsync(IAgentPipelineContext ctx, string rawJson);  
    }
}
