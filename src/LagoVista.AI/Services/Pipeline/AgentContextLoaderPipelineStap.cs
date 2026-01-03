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
            IPromptKnowledgeProviderInitializerPipelineStep next,
            IAgentPipelineContextValidator validator,
            IAgentContextManager contextManager,
            IAdminLogger adminLogger) : base(next, validator, adminLogger)
        {
            _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
        }

        protected override PipelineSteps StepType => PipelineSteps.AgentContextLoader;

        protected override async Task<InvokeResult<IAgentPipelineContext>> ExecuteStepAsync(IAgentPipelineContext ctx)
        {
            var agentContext = await _contextManager.GetAgentContextWithSecretsAsync(ctx.Session.AgentContext.Id, ctx.Envelope.Org, ctx.Envelope.User);

            var role = agentContext.Roles.SingleOrDefault(cc => cc.Id == ctx.Session.Role.Id);
            if(role == null)
            {
                return InvokeResult<IAgentPipelineContext>.FromError("Conversation Context specified in session not found in Agent Context.", "AGENT_CTX_LOADER_CONVERSATION_CONTEXT_NOT_FOUND_IN_AGENT_CONTEXT");
            }

            var mode = agentContext.AgentModes.SingleOrDefault(cc => cc.Id == ctx.Session.AgentMode?.Id);
            if(mode == null)
                mode = agentContext.AgentModes.SingleOrDefault(cc => cc.Key == ctx.Session.Mode);

            if(mode == null) InvokeResult<IAgentPipelineContext>.FromError("Mode was not found in Agent Context.", "AGENT_CTX_LOADER_COULD_NOT_RESOLVE_MODE");


            ctx.AttachAgentContext(agentContext, role, mode);
         
            return InvokeResult<IAgentPipelineContext>.Create(ctx);  
        }
    }
}
