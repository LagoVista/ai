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
    public sealed class ClientToolCallSessionRestorerPipelineStep : PipelineStep, IClientToolCallSessionRestorerPipelineStep
    {
        private readonly IAgentSessionManager _sessionManager;

        public ClientToolCallSessionRestorerPipelineStep(
            IClientToolContinuationResolverPipelineStep next,
            IAgentSessionManager sessionManager,
            IToolCallManifestRepo repo,
            IAgentPipelineContextValidator validator,
            IAdminLogger adminLogger) : base(next, validator, adminLogger)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        }

        protected override PipelineSteps StepType => PipelineSteps.ClientToolCallSessionRestorer;

        protected override async Task<InvokeResult<IAgentPipelineContext>> ExecuteStepAsync(IAgentPipelineContext ctx)
        {        
            var session = await _sessionManager.GetAgentSessionAsync(ctx.Envelope.SessionId, ctx.Envelope.Org, ctx.Envelope.User);

            // For Tool Continuations we maintain the same turn as before
            var previousTurn = session.Turns.FirstOrDefault(t => t.Id == ctx.Envelope.PreviousTurnId);
            if (previousTurn == null)
                return InvokeResult<IAgentPipelineContext>.FromError("Turn Id not found in Previous Turns", "AGENT_SESSION_RESTORE_NO_PREVIOUS_TURN");

            ctx.AttachClientToolSession(session, previousTurn);

            return InvokeResult<IAgentPipelineContext>.Create(ctx);
        }
    }
}
