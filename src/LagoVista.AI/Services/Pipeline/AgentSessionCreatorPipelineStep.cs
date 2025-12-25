using System;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services.Pipeline
{
    /// <summary>
    /// AGN-032 Step: AgentSessionCreator
    ///
    /// Expects:
    /// - A new-session request already selected by <c>AgentRequestHandlerPipelineStep</c>.
    /// - <see cref="AgentPipelineContext.Request"/> is present and contains the user's instruction.
    ///
    /// Updates:
    /// - Creates a new <see cref="AgentSession"/> via <see cref="IAgentSessionFactory"/> (no persistence in this step).
    /// - Creates the first <see cref="AgentSessionTurn"/> for that session.
    /// - Sets <see cref="AgentPipelineContext.Session"/>, <see cref="AgentPipelineContext.Turn"/>,
    ///   and <c>Request.CurrentTurnId</c>.
    ///
    /// Emits:
    /// - Publishes high-level orchestrator events for <c>SessionStarted</c> and <c>TurnCreated</c>.
    ///
    /// Next:
    /// - <c>SessionContextResolver</c> (<see cref="ISessionContextResolverPipelineStep"/>).
    /// </summary>
    public sealed class AgentSessionCreatorPipelineStep : IAgentSessionCreatorPipelineStep
    {
        private readonly ISessionContextResolverPipelineStep _next; // Temporary seam until SessionContextResolverPipelineStep exists.
        private readonly IAdminLogger _adminLogger;

        private readonly IAgentSessionFactory _sessionFactory;
        private readonly INotificationPublisher _notificationPublisher;

        public AgentSessionCreatorPipelineStep(
            ISessionContextResolverPipelineStep next,
            IAgentSessionFactory sessionFactory,
            INotificationPublisher notificationPublisher,
            IAdminLogger adminLogger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
            _notificationPublisher = notificationPublisher ?? throw new ArgumentNullException(nameof(notificationPublisher));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx)
        {
            if (ctx == null)
            {
                _adminLogger.AddError("[AgentSessionCreatorPipelineStep__ExecuteAsync]", "AgentPipelineContext cannot be null.");
                return InvokeResult<AgentPipelineContext>.FromError("AgentPipelineContext cannot be null.", "AGENT_PIPELINE_NULL_CTX");
            }

            if (ctx.Request == null)
            {
                const string msg = "AgentExecutionRequest cannot be null.";
                _adminLogger.AddError("[AgentSessionCreatorPipelineStep__ExecuteAsync__ValidateRequest]", msg);
                return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_SESSION_CREATE_NULL_REQUEST");
            }

            if (string.IsNullOrWhiteSpace(ctx.Request.Instruction) && ctx.Request.InputArtifacts?.Count == 0)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "Instruction is required.",
                    "AGENT_SESSION_RESTORE_MISSING_INSTRUCTION_AND_NO_ACTIVE_FILES");
            }

            _adminLogger.Trace("[AgentSessionCreatorPipelineStep__ExecuteAsync] - Creating new session.",
                (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                (ctx.Org?.Id ?? string.Empty).ToKVP("TenantId"),
                (ctx.User?.Id ?? string.Empty).ToKVP("UserId"));

            var session = await _sessionFactory.CreateSession(ctx.Request, OperationKinds.Code, ctx.Org, ctx.User);
            var turn = _sessionFactory.CreateTurnForNewSession(session, ctx.Request, ctx.Org, ctx.User);

            ctx.Session = session;
            ctx.Turn = turn;
            ctx.Request.SessionId = session.Id;
            ctx.Request.TurnId = turn.Id;

            if (ctx.CancellationToken.IsCancellationRequested)
            {
                return InvokeResult<AgentPipelineContext>.Abort();
            }

            await PublishSessionStartedAsync(session);
            await PublishTurnCreatedAsync(session, turn);

            _adminLogger.Trace("[AgentSessionCreatorPipelineStep__ExecuteAsync] - Session created and first turn initialized.",
                (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                (session?.Id ?? string.Empty).ToKVP("SessionId"),
                (turn?.Id ?? string.Empty).ToKVP("TurnId"));

            // Next step (per AGN-032): SessionContextResolver (still routed through _next seam for now).
            return await _next.ExecuteAsync(ctx);
        }
        private async Task PublishSessionStartedAsync(AgentSession session)
        {
            var evt = new AptixOrchestratorEvent
            {
                SessionId = session.Id,
                TurnId = null,
                Stage = "SessionStarted",
                Status = "pending",
                Message = "Session created.",
                ElapsedMs = null,
                Timestamp = DateTime.UtcNow.ToJSONString()
            };

            await _notificationPublisher.PublishAsync(Targets.WebSocket, Channels.Entity, session.Id, evt, NotificationVerbosity.Normal);
        }

        private async Task PublishTurnCreatedAsync(AgentSession session, AgentSessionTurn turn)
        {
            var evt = new AptixOrchestratorEvent
            {
                SessionId = session.Id,
                TurnId = turn.Id,
                Stage = "TurnCreated",
                Status = "pending",
                Message = "Turn created.",
                ElapsedMs = null,
                Timestamp = DateTime.UtcNow.ToJSONString()
            };

            await _notificationPublisher.PublishAsync(Targets.WebSocket, Channels.Entity, session.Id, evt, NotificationVerbosity.Normal);
        }
    }
}
