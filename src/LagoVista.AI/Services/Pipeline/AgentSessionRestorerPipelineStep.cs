using System;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services.Pipeline
{
    public sealed class AgentSessionRestorerPipelineStep : PipelineStep, IAgentSessionRestorerPipelineStep
    {
        private readonly IAgentSessionManager _sessionManager;
        private readonly IAgentSessionFactory _sessionFactory;

        public AgentSessionRestorerPipelineStep(
            IAgentContextLoaderPipelineStap next,
            IAgentSessionManager sessionManager,
            IAgentSessionFactory sessionFactory,
            IAdminLogger adminLogger) : base(next, adminLogger)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        }

        protected override PipelineSteps StepType => PipelineSteps.SessionRestorer;

        protected override async Task<InvokeResult<IAgentPipelineContext>> ExecuteStepAsync(IAgentPipelineContext ctx)
        {
            // throws record not found exception if session is not available.
            var session = await _sessionManager.GetAgentSessionAsync(ctx.Envelope.SessionId, ctx.Envelope.Org, ctx.Envelope.User);

            var previousTurn = session.Turns.FirstOrDefault(t => t.Id == ctx.Envelope.TurnId);
            if (previousTurn == null)
                return InvokeResult<IAgentPipelineContext>.FromError("Turn Id not found in Previous Turns", "AGENT_SESSION_RESTORE_NO_PREVIOUS_TURN");
         
            var turn = _sessionFactory.CreateTurnForExistingSession(ctx, session);
            if(String.IsNullOrEmpty(previousTurn.OpenAIResponseId))
                return InvokeResult<IAgentPipelineContext>.FromError("Previous Turn MUST have OpenAIResponseId but is missing", "AGENT_SESSION_RESTORE_NO_PREVIOUS_RESPONSE_ID");

            turn.PreviousOpenAIResponseId = previousTurn.OpenAIResponseId;

            ctx.AttachSession(session, turn);

            return InvokeResult<IAgentPipelineContext>.Create(ctx);
        }
    }
}
