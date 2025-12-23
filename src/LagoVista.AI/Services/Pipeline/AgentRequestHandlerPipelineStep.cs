using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.UserAdmin.Interfaces.Managers;

namespace LagoVista.AI.Services.Pipeline
{
    /// <summary>
    /// AGN-032 Step: AgentRequestHandler
    ///
    /// Expects:
    /// - Transport request and org/user identity.
    ///
    /// Updates:
    /// - Constructs initial AgentPipelineContext.
    /// - Normalizes request fields needed by downstream steps.
    /// - Routes to one of the three session paths:
    ///     - AgentSessionCreatorPipelineStep
    ///     - AgentSessionRestorerPipelineStep
    ///     - ClientToolCallSessionRestorerPipelineStep
    ///
    /// Next:
    /// - AgentSessionCreatorPipelineStep OR AgentSessionRestorerPipelineStep OR ClientToolCallSessionRestorerPipelineStep
    /// </summary>
    public sealed class AgentRequestHandlerPipelineStep : IAgentRequestHandlerStep
    {
        private readonly IAgentSessionCreatorPipelineStep _sessionCreator;
        private readonly IAgentSessionRestorerPipelineStep _sessionRestorer;
        private readonly IClientToolCallSessionRestorerPipelineStep _toolSessionRestorer;
        private readonly IAgentSessionManager _agentSessionManager;
        private readonly IAdminLogger _adminLogger;
        private readonly IAgentStreamingContext _agentStreamingContext;

        public AgentRequestHandlerPipelineStep(
            IAgentSessionCreatorPipelineStep sessionCreator,
            IAgentSessionRestorerPipelineStep sessionRestorer,
            IClientToolCallSessionRestorerPipelineStep toolSessionRestorer,
            IAdminLogger adminLogger,
            IAgentSessionManager agentSessionManager,
            IAgentStreamingContext agentStreamingContext)
        {
            _sessionCreator = sessionCreator ?? throw new ArgumentNullException(nameof(sessionCreator));
            _sessionRestorer = sessionRestorer ?? throw new ArgumentNullException(nameof(sessionRestorer));
            _toolSessionRestorer = toolSessionRestorer ?? throw new ArgumentNullException(nameof(toolSessionRestorer));
            _agentSessionManager = agentSessionManager ?? throw new ArgumentNullException(nameof(agentSessionManager));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
        }

        public async Task<InvokeResult<AgentExecuteResponse>> HandleAsync(
            AgentExecuteRequest request,
            EntityHeader org,
            EntityHeader user,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                const string msg = "AgentExecuteRequest cannot be null.";
                _adminLogger.AddError("[AgentRequestHandlerPipelineStep__ExecuteAsync__ValidateRequest]", msg);
                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_REQ_NULL_REQUEST");
            }

            if (org == null || EntityHeader.IsNullOrEmpty(org))
            {
                const string msg = "Org identity is required.";
                _adminLogger.AddError("[AgentRequestHandlerPipelineStep__ExecuteAsync__ValidateRequest]", msg);
                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_REQ_MISSING_ORG");
            }

            if (user == null || EntityHeader.IsNullOrEmpty(user))
            {
                const string msg = "User identity is required.";
                _adminLogger.AddError("[AgentRequestHandlerPipelineStep__ExecuteAsync__ValidateRequest]", msg);
                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_REQ_MISSING_USER");
            }

            if (string.IsNullOrWhiteSpace(request.Instruction))
            {
                const string msg = "Instruction is required.";
                _adminLogger.AddError("[AgentRequestHandlerPipelineStep__ExecuteAsync__ValidateRequest]", msg);
                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_REQ_MISSING_INSTRUCTION");
            }

            var ctx = new AgentPipelineContext
            {
                CorrelationId = Guid.NewGuid().ToId(),
                Org = org,
                User = user,
                Request = request,
                CancellationToken = cancellationToken
            };

            _adminLogger.Trace("[AgentRequestHandlerPipelineStep__ExecuteAsync] - Handling agent request.",
                (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                (org?.Id ?? string.Empty).ToKVP("TenantId"),
                (user?.Id ?? string.Empty).ToKVP("UserId"),
                (request?.SessionId ?? string.Empty).ToKVP("SessionId"));

            // Transition table (user-provided):
            // - ToolResults has values => ClientToolCallSessionRestorer
            // - SessionId empty => AgentSessionCreator
            // - Otherwise => AgentSessionRestorer

            InvokeResult<AgentPipelineContext> result = null;

            var isNewSession = string.IsNullOrWhiteSpace(request.SessionId);
            if (isNewSession)
            {
                _adminLogger.Trace("[AgentRequestHandlerPipelineStep__ExecuteAsync__NewSession] - Routing: new session.",
                    (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                    (org?.Id ?? string.Empty).ToKVP("TenantId"),
                    (user?.Id ?? string.Empty).ToKVP("UserId"));

                await _agentStreamingContext.AddWorkflowAsync("Welcome to Aptix, Finding the next available agent...please wait...", cancellationToken);

                result = await _sessionCreator.ExecuteAsync(ctx);
            }

            var isToolContinuation = request.ToolResults != null && request.ToolResults.Count > 0;
            if (isToolContinuation)
            {
                _adminLogger.Trace("[AgentRequestHandlerPipelineStep__ExecuteAsync__ToolContinuation] - Routing: client tool continuation.",
                    (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                    (request.SessionId ?? string.Empty).ToKVP("SessionId"));

                await _agentStreamingContext.AddWorkflowAsync("Welcome Back to Aptix, resuming tool execution...", cancellationToken);


                result = await _toolSessionRestorer.ExecuteAsync(ctx);
            }

            if (!String.IsNullOrEmpty(request.SessionId) && !isToolContinuation)
            {
                _adminLogger.Trace("[AgentRequestHandlerPipelineStep__ExecuteAsync__FollowOn] - Routing: follow-on session.",
                (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                (org?.Id ?? string.Empty).ToKVP("TenantId"),
                (user?.Id ?? string.Empty).ToKVP("UserId"),
                (request.SessionId ?? string.Empty).ToKVP("SessionId"));

                await _agentStreamingContext.AddWorkflowAsync("Welcome Back to Aptix, let's get started...", cancellationToken);
                result = await _sessionRestorer.ExecuteAsync(ctx);
            }

            await _agentSessionManager.UpdateSessionAsync(ctx.Session, org, user);

            if(result.Successful)
                return InvokeResult<AgentExecuteResponse>.Create(ctx.Response);
            else
                return InvokeResult<AgentExecuteResponse>.FromInvokeResult(result.ToInvokeResult());
        }
    }
}
