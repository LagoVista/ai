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
            IOrganizationManager orgManager,
            IAdminLogger adminLogger) : base(next, adminLogger)
        {
            _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
            _orgManager = orgManager ?? throw new ArgumentNullException(nameof(orgManager));
        }

        protected override PipelineSteps StepType => PipelineSteps.AgentContextResolver; 

        protected override async Task<InvokeResult<AgentPipelineContext>> ExecuteStepAsync(AgentPipelineContext ctx)
        {
            var agentContextId = ctx.Envelope.AgentContextId;
            if (String.IsNullOrEmpty(agentContextId))
            {
                var org = await _orgManager.GetOrganizationAsync(ctx.Envelope.Org.Id, ctx.Envelope.Org, ctx.Envelope.User);
                if(EntityHeader.IsNullOrEmpty(org.DefaultAgentContext))
                {
                    return InvokeResult<AgentPipelineContext>.FromError(
                    "No AgentContextId provided and organization has no default AgentContext.",
                    "AGENT_CTX_RESOLVER_MISSING_AGENT_CONTEXT_ID_NO_ORG_DEFAULT");
                }
                agentContextId = org.DefaultAgentContext.Id;
            }

            var agentContext = await _contextManager.GetAgentContextAsync(agentContextId, ctx.Envelope.Org, ctx.Envelope.User);

            var conversationContextId = ctx.Envelope.ConversationContextId ?? agentContext.DefaultConversationContext?.Id;
            if(String.IsNullOrEmpty(conversationContextId) && agentContext.ConversationContexts.Any())
            {
                conversationContextId = agentContext.ConversationContexts.First().Id;
            }

            if (string.IsNullOrWhiteSpace(conversationContextId))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                "ConversationContextId not found.",
                "AGENT_CTX_RESOLVER_CONVERSATION_CONTEXT_ID_NOT_AVAILABLE");
            }

            var conversationContext = agentContext.ConversationContexts.FirstOrDefault(ctx => ctx.Id == conversationContextId); 
            if (conversationContext == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "ConversationContext not found.",
                    "AGENT_CTX_RESOLVER_CONVERSATION_CONTEXT_NOT_FOUND");
            }
            ctx.AttachAgentContext(agentContext, conversationContext);

            return InvokeResult<AgentPipelineContext>.Create(ctx);
        }
    }
}
