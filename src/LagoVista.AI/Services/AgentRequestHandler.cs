using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
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
    /// Takes a client-facing AgentExecuteRequest (browser/CLI/thick client),
    /// performs light validation, maps it into AgentPipelineContext, and routes
    /// into the AGN-032 step pipeline.
    ///
    /// Branching rules (per AGN-032 / user guidance):
    /// - If ToolResults contains values => client tool call continuation.
    /// - Else if SessionId is null/empty => brand new session.
    /// - Else => follow-on session.
    /// </summary>
    public sealed class AgentRequestHandler : IAgentRequestHandler
    {
        private readonly IAgentSessionCreatorPipelineStep _sessionCreator;
        private readonly IAgentSessionRestorerPipelineStep _sessionRestorer;
        private readonly IClientToolCallSessionRestorerPipelineStep _toolSessionRestorer;

        private readonly IAdminLogger _adminLogger;
        private readonly IAgentStreamingContext _agentStreamingContext;
        private readonly IOrganizationManager _orgManager;

        public AgentRequestHandler(
            IAgentSessionCreatorPipelineStep sessionCreator,
            IAgentSessionRestorerPipelineStep sessionRestorer,
            IClientToolCallSessionRestorerPipelineStep toolSessionRestorer,
            IAdminLogger adminLogger,
            IOrganizationManager orgManager,
            IAgentStreamingContext agentStreamingContext)
        {
            _sessionCreator = sessionCreator ?? throw new ArgumentNullException(nameof(sessionCreator));
            _sessionRestorer = sessionRestorer ?? throw new ArgumentNullException(nameof(sessionRestorer));
            _toolSessionRestorer = toolSessionRestorer ?? throw new ArgumentNullException(nameof(toolSessionRestorer));

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
            var ctx = new AgentPipelineContext
            {
                CorrelationId = Guid.NewGuid().ToId(),
                Org = org,
                User = user,
                Request = request,
                CancellationToken = cancellationToken
            };

            _adminLogger.Trace("[AgentRequestHandler__HandleAsync] - Handling agent request.",
                (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                (org?.Id ?? string.Empty).ToKVP("TenantId"),
                (user?.Id ?? string.Empty).ToKVP("UserId"),
                (request?.SessionId ?? string.Empty).ToKVP("SessionId"));

            var pipelineResult = await ExecuteAsync(ctx, cancellationToken);
            if (!pipelineResult.Successful)
            {
                _adminLogger.LogInvokeResult("[AgentRequestHandler__HandleAsync__PipelineFailed]", pipelineResult.ToInvokeResult(),
                    (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                    (request?.SessionId ?? string.Empty).ToKVP("SessionId"));

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
                const string msg = "AgentExecuteRequest cannot be null.";
                _adminLogger.AddError("[AgentRequestHandler__ExecuteAsync__ValidateRequest]", msg);
                return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_REQ_NULL_REQUEST");
            }

            if (string.IsNullOrWhiteSpace(request.Instruction))
            {
                const string msg = "Instruction is required.";
                _adminLogger.AddError("[AgentRequestHandler__ExecuteAsync__ValidateRequest]", msg);
                return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_REQ_MISSING_INSTRUCTION");
            }

            if (EntityHeader.IsNullOrEmpty(request.AgentContext))
            {
                var organization = await _orgManager.GetOrganizationAsync(ctx.Org.Id, ctx.Org, ctx.User);
                if (EntityHeader.IsNullOrEmpty(organization.DefaultAgentContext))
                {
                    const string msg = "AgentContext is required, this can either come from the request or be set as a default in the Owner settings.";
                    _adminLogger.AddError("[AgentRequestHandler__ExecuteAsync__ValidateRequest]", msg);
                    return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_REQ_MISSING_AGENT_CONTEXT");
                }

                request.AgentContext = organization.DefaultAgentContext;
            }


            var isToolContinuation = request.ToolResults != null && request.ToolResults.Count > 0;
            if (isToolContinuation)
            {
                await _agentStreamingContext.AddWorkflowAsync("Welcome Back to Aptix, resuming tool execution...", cancellationToken);

                _adminLogger.Trace("[AgentRequestHandler__ExecuteAsync] - Routing: client tool continuation.",
                    (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                    (ctx.SessionId ?? string.Empty).ToKVP("SessionId"));

                return await _toolSessionRestorer.ExecuteAsync(ctx);
            }

            var isNewSession = string.IsNullOrWhiteSpace(request.SessionId);
            if (isNewSession)
            {
                await _agentStreamingContext.AddWorkflowAsync("Welcome to Aptix, Finding the next available agent...please wait...", cancellationToken);

                _adminLogger.Trace("[AgentRequestHandler__ExecuteAsync] - Routing: new session.",
                    (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                    (ctx.Org?.Id ?? string.Empty).ToKVP("TenantId"),
                    (ctx.User?.Id ?? string.Empty).ToKVP("UserId"));

                return await _sessionCreator.ExecuteAsync(ctx);
            }

            await _agentStreamingContext.AddWorkflowAsync("Welcome Back to Aptix, let's get started...", cancellationToken);

            _adminLogger.Trace("[AgentRequestHandler__ExecuteAsync] - Routing: follow-on session.",
                (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                (ctx.Org?.Id ?? string.Empty).ToKVP("TenantId"),
                (ctx.User?.Id ?? string.Empty).ToKVP("UserId"),
                (ctx.SessionId ?? string.Empty).ToKVP("SessionId"));

            return await _sessionRestorer.ExecuteAsync(ctx);
        }
    }
}
