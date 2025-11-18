using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// Orchestrates Aptix agent sessions and turns.
    ///
    /// Responsibilities:
    /// - Validate incoming requests for new sessions and follow-up turns.
    /// - Create sessions/turns via IAgentSessionFactory.
    /// - Persist sessions/turns via IAgentSessionManager.
    /// - Execute turns via IAgentTurnExecutor.
    /// - Publish high-level AptixOrchestratorEvent notifications.
    ///
    /// Heavy lifting (RAG, LLM, transcripts) is delegated to collaborators so
    /// this class remains easy to mock and test.
    /// </summary>
    public class AgentOrchestrator : IAgentOrchestrator
    {
        private readonly IAgentSessionManager _sessionManager;
        private readonly IAgentSessionFactory _sessionFactory;
        private readonly IAgentTurnExecutor _turnExecutor;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly IAdminLogger _adminLogger;

        public AgentOrchestrator(IAgentSessionManager sessionManager, IAgentSessionFactory sessionFactory, IAgentTurnExecutor turnExecutor, INotificationPublisher notificationPublisher, IAdminLogger adminLogger)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
            _turnExecutor = turnExecutor ?? throw new ArgumentNullException(nameof(turnExecutor));
            _notificationPublisher = notificationPublisher ?? throw new ArgumentNullException(nameof(notificationPublisher));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task<InvokeResult<AgentExecutionResponse>> BeginNewSessionAsync(NewAgentExecutionSession request, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToId();

            _adminLogger.Trace("[AgentOrchestrator_BeginNewSessionAsync] Starting new session. " + $"correlationId={correlationId}, org={org?.Id}, user={user?.Id}");

            if (request == null)
            {
                const string msg = "NewAgentExecutionSession cannot be null.";
                _adminLogger.AddError("[AgentOrchestrator_BeginNewSessionAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecutionResponse>.FromError(msg, "AGENT_ORCH_NULL_REQUEST");
            }

            if (request.AgentContext == null || EntityHeader.IsNullOrEmpty(request.AgentContext))
            {
                const string msg = "AgentContext is required.";
                _adminLogger.AddError("[AgentOrchestrator_BeginNewSessionAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecutionResponse>.FromError(msg, "AGENT_ORCH_MISSING_AGENT_CONTEXT");
            }

            if (string.IsNullOrWhiteSpace(request.Instruction))
            {
                const string msg = "Instruction is required.";
                _adminLogger.AddError("[AgentOrchestrator_BeginNewSessionAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecutionResponse>.FromError(msg, "AGENT_ORCH_MISSING_INSTRUCTION");
            }

            var session = _sessionFactory.CreateSession(request, org, user);
            var turn = _sessionFactory.CreateTurnForNewSession(session, request, org, user);

            await _sessionManager.AddAgentSessionAsync(session, org, user);
            await _sessionManager.AddAgentSessionTurnAsync(session.Id, turn, org, user);

            await PublishSessionStartedAsync(session, org, user);
            await PublishTurnCreatedAsync(session, turn, org, user);

            var stopwatch = Stopwatch.StartNew();
            await PublishTurnExecutionStartedAsync(session, turn, org, user);

            var execResult = await _turnExecutor.ExecuteNewSessionTurnAsync(session, turn, request, org, user, cancellationToken);

            stopwatch.Stop();

            if (!execResult.Successful)
            {
                var warnings = execResult.Result?.Warnings ?? new List<string>();

                await _sessionManager.FailAgentSessionTurnAsync(session.Id, turn.Id, null, execResult.Errors.Select(er => er.Message).ToList(), warnings, org, user);

                await PublishTurnFailedAsync(session, turn, execResult, stopwatch.ElapsedMilliseconds, org, user);

                return execResult;
            }

            var response = execResult.Result;

            await _sessionManager.CompleteAgentSessionTurnAsync(session.Id, turn.Id, response.AgentAnswer, response.OpenAIResponseBlobUrl, response.OpenAIResponseId, response.Warnings, org, user);

            await PublishTurnCompletedAsync(session, turn, stopwatch.ElapsedMilliseconds, org, user);

            return execResult;
        }

        public async Task<InvokeResult<AgentExecutionResponse>> ExecuteTurnAsync(AgentExecutionRequest request, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToId();

            _adminLogger.Trace("[AgentOrchestrator_ExecuteTurnAsync] Starting follow-up turn. " + $"correlationId={correlationId}, org={org?.Id}, user={user?.Id}");

            if (request == null)
            {
                const string msg = "AgentExecutionRequest cannot be null.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteTurnAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecutionResponse>.FromError(msg, "AGENT_ORCH_NULL_REQUEST");
            }

            if (string.IsNullOrWhiteSpace(request.SessionId))
            {
                const string msg = "SessionId is required.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteTurnAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecutionResponse>.FromError(msg, "AGENT_ORCH_MISSING_SESSION_ID");
            }

            if (string.IsNullOrWhiteSpace(request.Instruction))
            {
                const string msg = "Instruction is required.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteTurnAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecutionResponse>.FromError(msg, "AGENT_ORCH_MISSING_INSTRUCTION");
            }

            var session = await _sessionManager.GetAgentSessionAsync(request.SessionId, org, user);

            if (session == null)
            {
                const string msg = "Session not found.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteTurnAsync__LoadSession]", msg);

                return InvokeResult<AgentExecutionResponse>.FromError(msg, "AGENT_ORCH_SESSION_NOT_FOUND");
            }

            AgentSessionTurn previousTurn;
            if (!string.IsNullOrWhiteSpace(request.PreviousTurnId))
            {
                previousTurn = await _sessionManager.GetAgentSessionTurnAsync(request.SessionId, request.PreviousTurnId, org, user);
            }
            else
            {
                previousTurn = await _sessionManager.GetLastAgentSessionTurnAsync(request.SessionId, org, user);
            }

            if (previousTurn == null)
            {
                const string msg = "No previous turns found for this session.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteTurnAsync__PreviousTurn]", msg);

                return InvokeResult<AgentExecutionResponse>.FromError(msg, "AGENT_ORCH_NO_PREVIOUS_TURN");
            }

            var turn = _sessionFactory.CreateTurnForExistingSession(session, request, org, user);

            turn.SequenceNumber = previousTurn.SequenceNumber + 1;
            turn.ConversationId = string.IsNullOrWhiteSpace(previousTurn.ConversationId) ? Guid.NewGuid().ToId() : previousTurn.ConversationId;
            turn.PreviousOpenAIResponseId = previousTurn.OpenAIResponseId;

            await _sessionManager.AddAgentSessionTurnAsync(session.Id, turn, org, user);

            await PublishTurnCreatedAsync(session, turn, org, user);

            var stopwatch = Stopwatch.StartNew();
            await PublishTurnExecutionStartedAsync(session, turn, org, user);

            var execResult = await _turnExecutor.ExecuteFollowupTurnAsync(session, turn, request, org, user, cancellationToken);

            stopwatch.Stop();

            if (!execResult.Successful)
            {
                var warnings = execResult.Result?.Warnings ?? new List<string>();

                await _sessionManager.FailAgentSessionTurnAsync(session.Id, turn.Id, null, execResult.Errors.Select(er => er.Message).ToList(), warnings, org, user);

                await PublishTurnFailedAsync(session, turn, execResult, stopwatch.ElapsedMilliseconds, org, user);

                return execResult;
            }

            var response = execResult.Result;

            await _sessionManager.CompleteAgentSessionTurnAsync(session.Id, turn.Id, response.AgentAnswer, response.OpenAIResponseBlobUrl, response.OpenAIResponseId, response.Warnings, org, user);

            await PublishTurnCompletedAsync(session, turn, stopwatch.ElapsedMilliseconds, org, user);

            return execResult;
        }

        private async Task PublishSessionStartedAsync(AgentSession session, EntityHeader org, EntityHeader user)
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

        private async Task PublishTurnCreatedAsync(AgentSession session, AgentSessionTurn turn, EntityHeader org, EntityHeader user)
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

        private async Task PublishTurnExecutionStartedAsync(AgentSession session, AgentSessionTurn turn, EntityHeader org, EntityHeader user)
        {
            var evt = new AptixOrchestratorEvent
            {
                SessionId = session.Id,
                TurnId = turn.Id,
                Stage = "TurnExecutionStarted",
                Status = "in-progress",
                Message = "Executing turn.",
                ElapsedMs = null,
                Timestamp = DateTime.UtcNow.ToJSONString()
            };

            await _notificationPublisher.PublishAsync(Targets.WebSocket, Channels.Entity, session.Id, evt, NotificationVerbosity.Normal);
        }

        private async Task PublishTurnCompletedAsync(AgentSession session, AgentSessionTurn turn, long elapsedMilliseconds, EntityHeader org, EntityHeader user)
        {
            var evt = new AptixOrchestratorEvent
            {
                SessionId = session.Id,
                TurnId = turn.Id,
                Stage = "TurnCompleted",
                Status = "completed",
                Message = "Turn completed successfully.",
                ElapsedMs = elapsedMilliseconds,
                Timestamp = DateTime.UtcNow.ToJSONString()
            };

            await _notificationPublisher.PublishAsync(Targets.WebSocket, Channels.Entity, session.Id, evt, NotificationVerbosity.Normal);
        }

        private async Task PublishTurnFailedAsync(AgentSession session, AgentSessionTurn turn, InvokeResult<AgentExecutionResponse> execResult, long elapsedMilliseconds, EntityHeader org, EntityHeader user)
        {
            var message = execResult.Errors != null && execResult.Errors.Count > 0 ? execResult.Errors[0].Message : "Turn failed.";

            var evt = new AptixOrchestratorEvent
            {
                SessionId = session.Id,
                TurnId = turn.Id,
                Stage = "TurnFailed",
                Status = "failed",
                Message = message,
                ElapsedMs = elapsedMilliseconds,
                Timestamp = DateTime.UtcNow.ToJSONString()
            };

            await _notificationPublisher.PublishAsync(Targets.WebSocket, Channels.Entity, session.Id, evt, NotificationVerbosity.Normal);
        }
    }
}
