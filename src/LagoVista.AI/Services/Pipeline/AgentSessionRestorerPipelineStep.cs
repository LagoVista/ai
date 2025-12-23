using System;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services.Pipeline
{
    /// <summary>
    /// AGN-032 Step: AgentSessionRestorer
    ///
    /// Expects:
    /// - A follow-on request already selected by <c>AgentRequestHandlerPipelineStep</c>.
    /// - <see cref="AgentPipelineContext.Request"/> is present.
    /// - <c>Request.SessionId</c> is non-empty.
    ///
    /// Updates:
    /// - Loads the existing <see cref="AgentSession"/>.
    /// - Resolves the previous turn (explicit <c>Request.PreviousTurnId</c> if provided, else last turn).
    /// - Appends a brand new <see cref="AgentSessionTurn"/> for the follow-on request.
    /// - Sets <see cref="AgentPipelineContext.Session"/>, <see cref="AgentPipelineContext.Turn"/>,
    ///   and <c>Request.CurrentTurnId</c>.
    /// - Stamps turn sequencing + continuation fields used by downstream execution.
    ///
    /// Next:
    /// - <c>SessionContextResolver</c> (<see cref="ISessionContextResolverPipelineStep"/>).
    /// </summary>
    public sealed class AgentSessionRestorerPipelineStep : IAgentSessionRestorerPipelineStep
    {
        private readonly ISessionContextResolverPipelineStep _next;
        private readonly IAgentSessionManager _sessionManager;
        private readonly IAgentSessionFactory _sessionFactory;
        private readonly IAdminLogger _adminLogger;

        public AgentSessionRestorerPipelineStep(
            ISessionContextResolverPipelineStep next,
            IAgentSessionManager sessionManager,
            IAgentSessionFactory sessionFactory,
            IAdminLogger adminLogger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx)
        {
            if (ctx == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "AgentPipelineContext cannot be null.",
                    "AGENT_SESSION_RESTORE_NULL_CONTEXT");
            }

            if (ctx.Request == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "AgentExecuteRequest is required.",
                    "AGENT_SESSION_RESTORE_MISSING_REQUEST");
            }

            if (ctx.Org == null || EntityHeader.IsNullOrEmpty(ctx.Org))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "Org is required.",
                    "AGENT_SESSION_RESTORE_MISSING_ORG");
            }

            if (ctx.User == null || EntityHeader.IsNullOrEmpty(ctx.User))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "User is required.",
                    "AGENT_SESSION_RESTORE_MISSING_USER");
            }

            if (string.IsNullOrWhiteSpace(ctx.Request.SessionId))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "SessionId is required.",
                    "AGENT_SESSION_RESTORE_MISSING_SESSION_ID");
            }

            if (string.IsNullOrWhiteSpace(ctx.Request.Instruction) && ctx.Request.ActiveFiles?.Count == 0)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "Instruction is required.",
                    "AGENT_SESSION_RESTORE_MISSING_INSTRUCTION_AND_NO_ACTIVE_FILES");
            }

            _adminLogger.Trace("[AgentSessionRestorerPipelineStep__ExecuteAsync] - Restoring existing session and appending a new turn.",
                (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                (ctx.Org?.Id ?? string.Empty).ToKVP("TenantId"),
                (ctx.User?.Id ?? string.Empty).ToKVP("UserId"),
                (ctx.Request.SessionId ?? string.Empty).ToKVP("SessionId"),
                (ctx.Request.PreviousTurnId ?? string.Empty).ToKVP("PreviousTurnId"));

            var session = await _sessionManager.GetAgentSessionAsync(ctx.Request.SessionId, ctx.Org, ctx.User);
            if (session == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "Session not found.",
                    "AGENT_SESSION_RESTORE_SESSION_NOT_FOUND");
            }

            ctx.Session = session;

            var previousTurn = ctx.Session.Turns.FirstOrDefault(t => t.Id == ctx.Request.PreviousTurnId);
            if (previousTurn == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "No previous turns found for this session.",
                    "AGENT_SESSION_RESTORE_NO_PREVIOUS_TURN");
            }

            var turn = _sessionFactory.CreateTurnForExistingSession(session, ctx.Request, ctx.Org, ctx.User);

            turn.SequenceNumber = session.Turns.Count + 1;
            turn.SessionId = session.Id;
            turn.PreviousOpenAIResponseId = previousTurn.OpenAIResponseId;
            ctx.Turn = turn;
            ctx.Request.CurrentTurnId = turn.Id;

            if (ctx.CancellationToken.IsCancellationRequested)
            {
                return InvokeResult<AgentPipelineContext>.Abort();
            }

            _adminLogger.Trace("[AgentSessionRestorerPipelineStep__ExecuteAsync] - Session restored and new turn appended.",
                (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                (session?.Id ?? string.Empty).ToKVP("SessionId"),
                (turn?.Id ?? string.Empty).ToKVP("TurnId"),
                (turn.SequenceNumber.ToString()).ToKVP("TurnSequence"));

            return await _next.ExecuteAsync(ctx);
        }
    }
}
