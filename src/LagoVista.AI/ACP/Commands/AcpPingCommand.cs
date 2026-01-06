using System;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.Core.Validation;

namespace LagoVista.AI.ACP.Commands
{
    [AcpCommand("acp.ping", "ACP Ping", "Health check command to validate ACP routing and execution.")]
    [AcpTriggers("acp.ping", "ping")]
    [AcpArgs(0, 0)]
    public sealed class AcpPingCommand : IAcpCommand
    {
        public Task<InvokeResult<IAgentPipelineContext>> ExecuteAsync(IAgentPipelineContext context, string[] args)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.SetTerminal("ACP ping handled.");
            
            var response = new AI.Models.ResponsePayload();
            response.AcpIntents.Add( 
                new Core.AI.Models.AcpIntent() { 
             
                IntentId = Guid.NewGuid().ToString(),
                Kind = Core.AI.Models.UiIntentKind.Notification,
                Message = "ACP Ping successful.",
                Title = "Ping",
            });

            context.SetResponsePayload(response);

            return Task.FromResult(InvokeResult<IAgentPipelineContext>.Create(context));
        }
    }
}