using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentExecuteResponseBuilder
    {
        Task<InvokeResult<AgentExecuteResponse>> BuildAsync(IAgentPipelineContext ctx);
    }
}
