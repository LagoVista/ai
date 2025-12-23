using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.UserAdmin.Interfaces.Managers;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// Takes a client-facing AgentRequestEnvelope (browser/CLI/thick client),
    /// performs light validation, maps it into AgentPipelineContext, and delegates
    /// to the next pipeline step (temporary seam until orchestrator is migrated).
    ///
    /// This class is intentionally thin so that:
    /// - Controllers have a single entry point regardless of client type.
    /// - The downstream pipeline only sees domain models, not transport DTOs.
    /// - Future client-specific response shaping can be added here without
    ///   impacting the orchestration pipeline.
    /// </summary>
    public class AgentRequestHandler : IAgentRequestHandler
    {
        private readonly IAgentOrchestrator _next;
        private readonly IAdminLogger _adminLogger;
        private readonly IAgentStreamingContext _agentStreamingContext;
        private readonly IOrganizationManager _orgManager;

        public AgentRequestHandler(
            IAgentOrchestrator next,
            IAdminLogger adminLogger,
            IOrganizationManager orgManager,
            IAgentStreamingContext agentStreamingContext)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
            _orgManager = orgManager ?? throw new ArgumentNullException(nameof(orgManager));
        }

        public async Task<InvokeResult<AgentExecuteResponse>> HandleAsync(
            AgentExecuteRequest request,
            EntityHeader org,
            EntityHeader user,
            CancellationToken cancellationToken = default)
        {
            var ctx = new AgentPipelineContext()
            {
                CorrelationId = Guid.NewGuid().ToId(),
                Org = org,
                User = user,
                Request = request,
                ConversationId = request?.ConversationId,
                CancellationToken = cancellationToken
            };


            _adminLogger.Trace("[AgentRequestHandler_HandleAsync] Handling agent request. " +
                               $"correlationId={ctx.CorrelationId}, org={org?.Id}, user={user?.Id}, sessionId={request?.ConversationId ?? "<null>"}");

            var pipelineResult = await ExecuteAsync(ctx, cancellationToken);
            if (!pipelineResult.Successful)
            {
                return InvokeResult<AgentExecuteResponse>.FromInvokeResult(pipelineResult.ToInvokeResult());
            }

            return InvokeResult<AgentExecuteResponse>.Create(ctx.Response);
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(
            AgentPipelineContext ctx,
            CancellationToken cancellationToken = default)
        {
            if (ctx == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError("AgentPipelineContext cannot be null.", "AGENT_REQ_NULL_CONTEXT");
            }

            var request = ctx.Request;

            if (request == null)
            {
                const string msg = "AgentRequestEnvelope cannot be null.";
                _adminLogger.AddError("[AgentRequestHandler_HandleAsync__ValidateRequest]", msg);

                return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_REQ_NULL_REQUEST");
            }

            if (string.IsNullOrWhiteSpace(request.Instruction))
            {
                const string msg = "Instruction is required.";
                _adminLogger.AddError("[AgentRequestHandler_HandleAsync__ValidateRequest]", msg);

                return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_REQ_MISSING_INSTRUCTION");
            }

            if (EntityHeader.IsNullOrEmpty(request.AgentContext))
            {
                var organization = await _orgManager.GetOrganizationAsync(ctx.Org.Id, ctx.Org, ctx.User);
                if (EntityHeader.IsNullOrEmpty(organization.DefaultAgentContext))
                {
                    const string msg = "AgentContext is required, this can either come from the request or be set as a default in the Owner settings.";
                    _adminLogger.AddError("[AgentRequestHandler_HandleAsync__ValidateRequest]", msg);
                    return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_REQ_MISSING_AGENT_CONTEXT");
                }

                request.AgentContext = organization.DefaultAgentContext;
            }

            ctx.ConversationId = request.ConversationId;

            var isNewSession = string.IsNullOrWhiteSpace(request.ConversationId);

            if (isNewSession)
            {
                return await HandleNewSessionAsync(ctx, cancellationToken);
            }

            return await HandleFollowupTurnAsync(ctx, cancellationToken);
        }

        private async Task<InvokeResult<AgentPipelineContext>> HandleNewSessionAsync(
            AgentPipelineContext ctx,
            CancellationToken cancellationToken)
        {
            await _agentStreamingContext.AddWorkflowAsync("Welcome to Aptix, Finding the next available agent...please wait...", cancellationToken);

            _adminLogger.Trace("[AgentRequestHandler_HandleNewSessionAsync] Normalizing new session request. " +
                               $"correlationId={ctx.CorrelationId}, org={ctx.Org?.Id}, user={ctx.User?.Id}");

            if (ctx.Request.AgentContext == null || EntityHeader.IsNullOrEmpty(ctx.Request.AgentContext))
            {
                const string msg = "AgentContext is required for a new session.";
                _adminLogger.AddError("[AgentRequestHandler_HandleNewSessionAsync__ValidateRequest]", msg);

                return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_REQ_MISSING_AGENT_CONTEXT");
            }

            return await _next.ExecuteAsync(ctx);
        }

        private async Task<InvokeResult<AgentPipelineContext>> HandleFollowupTurnAsync(
            AgentPipelineContext ctx,
            CancellationToken cancellationToken)
        {
            await _agentStreamingContext.AddWorkflowAsync("Welcome Back to Aptix, let's get started...", cancellationToken);

            _adminLogger.Trace("[AgentRequestHandler_HandleFollowupTurnAsync] Normalizing follow-up turn request. " +
                               $"correlationId={ctx.CorrelationId}, org={ctx.Org?.Id}, user={ctx.User?.Id}, conversationId={ctx.Request.ConversationId}");

            if (string.IsNullOrWhiteSpace(ctx.Request.ConversationId))
            {
                const string msg = "ConversationId is required for a follow-up turn.";
                _adminLogger.AddError("[AgentRequestHandler_HandleFollowupTurnAsync__ValidateRequest]", msg);

                return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_REQ_MISSING_SESSION_ID");
            }

            return await _next.ExecuteAsync(ctx);
        }
    }
}
