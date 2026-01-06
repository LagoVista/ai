using System;
using System.Threading.Tasks;
using LagoVista.AI.ACP;
using LagoVista.AI.Interfaces;
using LagoVista.Core.Validation;

namespace LagoVista.AI.ACP.Commands
{
    [AcpCommand("acp.ping", "ACP Ping", "Health check command to validate ACP routing and execution.")]
    [AcpTriggers("acp.ping", "ping")]
    [AcpArgs(0, 0)]
    public sealed class PingCommand : IAcpCommand
    {
        public Task<InvokeResult<IAgentPipelineContext>> ExecuteAsync(IAgentPipelineContext context, string[] args)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.SetTerminal("ACP ping handled.");

            return Task.FromResult(InvokeResult<IAgentPipelineContext>.Create(context));
        }
    }
}