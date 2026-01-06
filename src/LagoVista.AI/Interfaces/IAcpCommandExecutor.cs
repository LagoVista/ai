using LagoVista.AI.ACP;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IAcpCommandExecutor
    {
        Task<InvokeResult<IAgentPipelineContext>> ExecuteAsync(string commandId, IAgentPipelineContext context, string[] args);
    }
}
