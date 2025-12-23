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
    public sealed class AgentSessionRestorerPipelineStep : IAgentSessionRestorerPipelineStep
    {
        private readonly IAgentOrchestrator _next;
        private readonly IAdminLogger _adminLogger;

        public AgentSessionRestorerPipelineStep(IAgentOrchestrator next, IAdminLogger adminLogger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx)
        {
            if (ctx == null)
            {
                _adminLogger.AddError("[AgentSessionRestorerPipelineStep__ExecuteAsync]", "AgentPipelineContext cannot be null.");
                return InvokeResult<AgentPipelineContext>.FromError("AgentPipelineContext cannot be null.", "AGENT_PIPELINE_NULL_CTX");
            }

            _adminLogger.Trace("[AgentSessionRestorerPipelineStep__ExecuteAsync] - Restoring existing session and appending a new turn.",
                (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                (ctx.Org?.Id ?? string.Empty).ToKVP("TenantId"),
                (ctx.User?.Id ?? string.Empty).ToKVP("UserId"),
                (ctx.SessionId ?? string.Empty).ToKVP("SessionId"));

            return await _next.ExecuteAsync(ctx);
        }
    }
}
