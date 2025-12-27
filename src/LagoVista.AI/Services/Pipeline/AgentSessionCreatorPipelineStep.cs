using System;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services.Pipeline
{
    public sealed class AgentSessionCreatorPipelineStep : PipelineStep, IAgentSessionCreatorPipelineStep
    {
        private readonly IAgentSessionFactory _sessionFactory;

        public AgentSessionCreatorPipelineStep(
            IPromptKnowledgeProviderInitializerPipelineStep next,
            IAgentSessionFactory sessionFactory,
            IAgentPipelineContextValidator validator,
            IAdminLogger adminLogger) : base(next, validator, adminLogger)
        {
            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        }

        protected override PipelineSteps StepType => PipelineSteps.AgentSessionCreator;

        protected override async Task<InvokeResult<IAgentPipelineContext>> ExecuteStepAsync(IAgentPipelineContext ctx)
        {
            var session = await _sessionFactory.CreateSession(ctx);
            var turn = _sessionFactory.CreateTurnForNewSession(ctx, session);
            session.Turns.Add(turn);
            ctx.AttachSession(session, turn);
            
            return InvokeResult<IAgentPipelineContext>.Create(ctx);
        }
    }
}
