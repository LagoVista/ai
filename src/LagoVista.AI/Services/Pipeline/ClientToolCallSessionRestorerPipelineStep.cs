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
    /// AGN-032 Step: ClientToolCallSessionRestorer
    ///
    /// Expects:
    /// - A client-tool continuation request already selected by <c>AgentRequestHandlerPipelineStep</c>.
    /// - <see cref="AgentPipelineContext.Request"/> is present.
    /// - <c>Request.SessionId</c> is non-empty.
    /// - <c>Request.ToolResults</c> contains one or more entries produced by the client.
    ///
    /// Updates:
    /// - Loads the existing <see cref="AgentSession"/>.
    /// - Restores the previously-created <see cref="AgentSessionTurn"/> associated with
    ///   <c>Request.PreviousTurnId</c> instead of creating a new turn.
    /// - Normalizes continuation fields on the turn so downstream execution can resume
    ///   the same logical turn.
    /// - Sets <see cref="AgentPipelineContext.Session"/>, <see cref="AgentPipelineContext.Turn"/>,
    ///   and <c>Request.CurrentTurnId</c>.
    ///
    /// Notes:
    /// - Unlike <c>AgentSessionRestorerPipelineStep</c>, this step does NOT append a new turn.
    ///   The client is completing a previously-declared tool call within the same turn.
    ///
    /// Next:
    /// - <c>SessionContextResolver</c> (<see cref="ISessionContextResolverPipelineStep"/>).
    /// </summary>
    public sealed class ClientToolCallSessionRestorerPipelineStep : IClientToolCallSessionRestorerPipelineStep
    {
        private readonly ISessionContextResolverPipelineStep _next;
        private readonly IAgentSessionManager _sessionManager;
        private readonly IAdminLogger _adminLogger;

        public ClientToolCallSessionRestorerPipelineStep(
            ISessionContextResolverPipelineStep next,
            IAgentSessionManager sessionManager,
            IAdminLogger adminLogger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx)
        {
            if (ctx == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "AgentPipelineContext cannot be null.",
                    "CLIENT_TOOL_SESSION_RESTORE_NULL_CONTEXT");
            }

            if (ctx.Request == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "AgentExecuteRequest is required.",
                    "CLIENT_TOOL_SESSION_RESTORE_MISSING_REQUEST");
            }

            if (ctx.Org == null || EntityHeader.IsNullOrEmpty(ctx.Org))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "Org is required.",
                    "CLIENT_TOOL_SESSION_RESTORE_MISSING_ORG");
            }

            if (ctx.User == null || EntityHeader.IsNullOrEmpty(ctx.User))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "User is required.",
                    "CLIENT_TOOL_SESSION_RESTORE_MISSING_USER");
            }

            if (string.IsNullOrWhiteSpace(ctx.Request.SessionId))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "SessionId is required.",
                    "CLIENT_TOOL_SESSION_RESTORE_MISSING_SESSION_ID");
            }

            if (string.IsNullOrWhiteSpace(ctx.Request.Instruction) && ctx.Request.ActiveFiles?.Count == 0)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "Instruction is required when no active files are present.",
                    "CLIENT_TOOL_SESSION_RESTORE_MISSING_INSTRUCTION_AND_NO_ACTIVE_FILES");
            }

            _adminLogger.Trace("[ClientToolCallSessionRestorerPipelineStep__ExecuteAsync] - Restoring session for client tool continuation.",
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
                    "CLIENT_TOOL_SESSION_RESTORE_SESSION_NOT_FOUND");
            }

            ctx.Session = session;

            // Client tool continuation resumes an existing turn; no new turn is created.
            var turn = ctx.Session.Turns.FirstOrDefault(t => t.Id == ctx.Request.PreviousTurnId);
            if (turn == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "Previous turn not found for this session.",
                    "CLIENT_TOOL_SESSION_RESTORE_NO_PREVIOUS_TURN");
            }

            // Normalize continuation metadata for downstream execution.
            turn.SequenceNumber = session.Turns.Count + 1;
            turn.SessionId = session.Id;
            turn.PreviousOpenAIResponseId = turn.OpenAIResponseId;

            ctx.Turn = turn;
            ctx.Request.CurrentTurnId = turn.Id;

            if (ctx.CancellationToken.IsCancellationRequested)
            {
                return InvokeResult<AgentPipelineContext>.Abort();
            }

            _adminLogger.Trace("[ClientToolCallSessionRestorerPipelineStep__ExecuteAsync] - Session restored for tool continuation.",
                (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                (session?.Id ?? string.Empty).ToKVP("SessionId"),
                (turn?.Id ?? string.Empty).ToKVP("TurnId"),
                (turn.SequenceNumber.ToString()).ToKVP("TurnSequence"));

            return await _next.ExecuteAsync(ctx);
        }
    }
}
