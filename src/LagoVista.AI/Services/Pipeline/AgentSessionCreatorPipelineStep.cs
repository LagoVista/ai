using System;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services.Pipeline
{
    public sealed class AgentSessionCreatorPipelineStep : IAgentSessionCreatorPipelineStep
    {
        private readonly IAgentOrchestrator _next;
        private readonly IAdminLogger _adminLogger;

        public AgentSessionCreatorPipelineStep(IAgentOrchestrator next, IAdminLogger adminLogger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx)
        {
            if (ctx == null)
            {
                _adminLogger.AddError("[AgentSessionCreatorPipelineStep__ExecuteAsync]", "AgentPipelineContext cannot be null.");
                return InvokeResult<AgentPipelineContext>.FromError("AgentPipelineContext cannot be null.", "AGENT_PIPELINE_NULL_CTX");
            }

            _adminLogger.Trace("[AgentSessionCreatorPipelineStep__ExecuteAsync] - Creating new session.",
                (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                (ctx.Org?.Id ?? string.Empty).ToKVP("TenantId"),
                (ctx.User?.Id ?? string.Empty).ToKVP("UserId"));

            return await _next.ExecuteAsync(ctx);
        }
    }
}
