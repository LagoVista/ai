using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.UserAdmin.Interfaces.Managers;

namespace LagoVista.AI.Services.Pipeline
{
    public sealed class AgentContextResolverPipelineStep : PipelineStep, IAgentContextResolverPipelineStep
    {
        private readonly IAgentContextManager _contextManager;
        private readonly IOrganizationManager _orgManager;
        public AgentContextResolverPipelineStep(
            IAgentSessionCreatorPipelineStep next,
            IAgentContextManager contextManager,
            IAgentPipelineContextValidator validator,
            IOrganizationManager orgManager,
            IAdminLogger adminLogger) : base(next, validator, adminLogger)
        {
            _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
            _orgManager = orgManager ?? throw new ArgumentNullException(nameof(orgManager));
        }

        protected override PipelineSteps StepType => PipelineSteps.AgentContextResolver; 

        protected override async Task<InvokeResult<IAgentPipelineContext>> ExecuteStepAsync(IAgentPipelineContext ctx)
        {
            var agentContextId = ctx.Envelope.AgentContextId;
            if (String.IsNullOrEmpty(agentContextId))
            {
                var org = await _orgManager.GetOrganizationAsync(ctx.Envelope.Org.Id, ctx.Envelope.Org, ctx.Envelope.User);
                if(EntityHeader.IsNullOrEmpty(org.DefaultAgentContext)) return InvokeResult<IAgentPipelineContext>.FromError("No AgentContextId provided and organization has no default AgentContext.", "AGENT_CTX_RESOLVER_MISSING_AGENT_CONTEXT_ID_NO_ORG_DEFAULT");
                 agentContextId = org.DefaultAgentContext.Id;
            }

            var agentContext = await _contextManager.GetAgentContextWithSecretsAsync(agentContextId, ctx.Envelope.Org, ctx.Envelope.User);

            var roleId = ctx.Envelope.RoleId ?? agentContext.DefaultRole?.Id;
            if (string.IsNullOrWhiteSpace(roleId))
            {
                return InvokeResult<IAgentPipelineContext>.FromError("RoleId not Provided and No Default for Agent Context.", "AGENT_CTX_RESOLVER_CONVERSATION_CONTEXT_ID_NOT_AVAILABLE");
            }

            var role = agentContext.Roles.FirstOrDefault(ctx => ctx.Id == roleId);
            if(role == null) return InvokeResult<IAgentPipelineContext>.FromError("Role not found.", "AGENT_CTX_RESOLVER_CONVERSATION_CONTEXT_NOT_FOUND");

            if (String.IsNullOrEmpty(ctx.AgentContext.DefaultMode.Id)) return InvokeResult<IAgentPipelineContext>.FromError("No Default Mode Provided on Agent Context.", "AGENT_CTX_RESOLVER_NO_DEFAULT_MODE_ON_AGENT_CONTEXT");

            var mode = agentContext.AgentModes.SingleOrDefault(cc => cc.Id == ctx.AgentContext.DefaultMode.Id); 
            if(mode == null) return InvokeResult<IAgentPipelineContext>.FromError("Mode not found.", "AGENT_CTX_RESOLVER_MODE_NOT_FOUND_IN_AGENT_CONTEXT");
            ctx.AttachAgentContext(agentContext, role, mode);

            return InvokeResult<IAgentPipelineContext>.Create(ctx);
        }
    }
}
