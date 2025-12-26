using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.Services.Pipeline
{
    public class AgentContextLoaderPipelineStap : PipelineStep, IAgentContextLoaderPipelineStap
    {
        private readonly IAgentContextManager _contextManager;
     
        public AgentContextLoaderPipelineStap(
            IContextProviderInitializerPipelineStep next,
            IAgentPipelineContextValidator validator,
            IAgentContextManager contextManager,
            IAdminLogger adminLogger) : base(next, validator, adminLogger)
        {
            _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
        }

        protected override PipelineSteps StepType => PipelineSteps.AgentContextLoader;

        protected override async Task<InvokeResult<IAgentPipelineContext>> ExecuteStepAsync(IAgentPipelineContext ctx)
        {
            var agentContext = await _contextManager.GetAgentContextAsync(ctx.Session.AgentContext.Id, ctx.Envelope.Org, ctx.Envelope.User);

            var conversationContext = agentContext.ConversationContexts.SingleOrDefault(cc => cc.Id == ctx.Session.ConversationContext.Id);
            if(conversationContext == null)
            {
                return InvokeResult<IAgentPipelineContext>.FromError("Conversation Context specified in session not found in Agent Context.", "AGENT_CTX_LOADER_CONVERSATION_CONTEXT_NOT_FOUND_IN_AGENT_CONTEXT");
            }

            ctx.AttachAgentContext(agentContext, conversationContext);
         
            return InvokeResult<IAgentPipelineContext>.Create(ctx);  
        }
    }
}
