using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// Takes a client-facing AgentRequestEnvelope (browser/CLI/thick client),
    /// performs light validation, maps it into NewAgentExecutionSession or
    /// AgentExecutionRequest, and delegates to the AgentOrchestrator.
    ///
    /// This class is intentionally thin so that:
    /// - Controllers have a single entry point regardless of client type.
    /// - The orchestrator only sees domain models, not transport DTOs.
    /// - Future client-specific response shaping can be added here without
    ///   impacting the orchestration pipeline.
    /// </summary>
    public class AgentRequestHandler : IAgentRequestHandler
    {
        private readonly IAgentOrchestrator _orchestrator;
        private readonly IAdminLogger _adminLogger;

        public AgentRequestHandler(IAgentOrchestrator orchestrator, IAdminLogger adminLogger)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task<InvokeResult<AgentExecutionResponse>> HandleAsync(AgentRequestEnvelope request, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToId();

            _adminLogger.Trace("[AgentRequestHandler_HandleAsync] Handling agent request. " + $"correlationId={correlationId}, org={org?.Id}, user={user?.Id}, sessionId={request?.SessionId ?? "<null>"}");

            if (request == null)
            {
                const string msg = "AgentRequestEnvelope cannot be null.";
                _adminLogger.AddError("[AgentRequestHandler_HandleAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecutionResponse>.FromError(msg, "AGENT_REQ_NULL_REQUEST");
            }

            if (string.IsNullOrWhiteSpace(request.Instruction))
            {
                const string msg = "Instruction is required.";
                _adminLogger.AddError("[AgentRequestHandler_HandleAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecutionResponse>.FromError(msg, "AGENT_REQ_MISSING_INSTRUCTION");
            }

            var isNewSession = string.IsNullOrWhiteSpace(request.SessionId);

            if (isNewSession)
            {
                return await HandleNewSessionAsync(request, org, user, correlationId, cancellationToken);
            }

            return await HandleFollowupTurnAsync(request, org, user, correlationId, cancellationToken);
        }

        private async Task<InvokeResult<AgentExecutionResponse>> HandleNewSessionAsync(AgentRequestEnvelope request, EntityHeader org, EntityHeader user, string correlationId, CancellationToken cancellationToken)
        {
            _adminLogger.Trace("[AgentRequestHandler_HandleNewSessionAsync] Normalizing new session request. " + $"correlationId={correlationId}, org={org?.Id}, user={user?.Id}");

            if (request.AgentContext == null || EntityHeader.IsNullOrEmpty(request.AgentContext))
            {
                const string msg = "AgentContext is required for a new session.";
                _adminLogger.AddError("[AgentRequestHandler_HandleNewSessionAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecutionResponse>.FromError(msg, "AGENT_REQ_MISSING_AGENT_CONTEXT");
            }

            var sessionRequest = new NewAgentExecutionSession
            {
                OperationKind = request.OperationKind,
                AgentContext = request.AgentContext,
                ConversationContext = request.ConversationContext,
                WorkspaceId = request.WorkspaceId,
                Repo = request.Repo,
                Language = request.Language,
                Instruction = request.Instruction,
                ActiveFiles = request.ActiveFiles,
                RagFilters = request.RagFilters
            };

            return await _orchestrator.BeginNewSessionAsync(sessionRequest, org, user, cancellationToken);
        }

        private async Task<InvokeResult<AgentExecutionResponse>> HandleFollowupTurnAsync(AgentRequestEnvelope request, EntityHeader org, EntityHeader user, string correlationId, CancellationToken cancellationToken)
        {
            _adminLogger.Trace("[AgentRequestHandler_HandleFollowupTurnAsync] Normalizing follow-up turn request. " + $"correlationId={correlationId}, org={org?.Id}, user={user?.Id}, sessionId={request.SessionId}");

            if (string.IsNullOrWhiteSpace(request.SessionId))
            {
                const string msg = "SessionId is required for a follow-up turn.";
                _adminLogger.AddError("[AgentRequestHandler_HandleFollowupTurnAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecutionResponse>.FromError(msg, "AGENT_REQ_MISSING_SESSION_ID");
            }

            var turnRequest = new AgentExecutionRequest
            {
                SessionId = request.SessionId,
                PreviousTurnId = request.PreviousTurnId,
                Instruction = request.Instruction,
                ActiveFiles = request.ActiveFiles,
                RagFilters = request.RagFilters
            };

            return await _orchestrator.ExecuteTurnAsync(turnRequest, org, user, cancellationToken);
        }
    }
}
