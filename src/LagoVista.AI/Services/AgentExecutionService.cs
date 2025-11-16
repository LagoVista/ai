using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.Core;
using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services
{
    public class AgentExecutionService : IAgentExecutionService
    {
        private readonly IAgentContextManager _agentContextManager;
        private readonly IRagAnswerService _ragAnswerService;
        private readonly IAdminLogger _adminLogger;

        public AgentExecutionService(IAgentContextManager agentContextManager, IRagAnswerService ragAnswerService,
            IAdminLogger adminLogger)
        {
            _agentContextManager = agentContextManager
                ?? throw new ArgumentNullException(nameof(agentContextManager));
            _ragAnswerService = ragAnswerService
                ?? throw new ArgumentNullException(nameof(ragAnswerService));
            _adminLogger = adminLogger
                ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task<InvokeResult<AgentExecuteResponse>> ExecuteAsync(AgentExecuteRequest request,
            EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToId();

            _adminLogger.Trace(
                $"[AgentExecutionService_ExecuteAsync] Starting execution. CorrelationId={correlationId}, " +
                $"Org={org?.Id}, User={user?.Id}, Mode={request?.Mode}, AgentContext={request?.AgentContext?.Id}");

            if (request == null)
            {
                const string msg = "AgentExecuteRequest cannot be null.";
                _adminLogger.AddError("[AgentExecutionService_ExecuteAsync__ValidateRequest]", msg);
                return InvokeResult<AgentExecuteResponse>.FromError(msg);
            }

            if (request.AgentContext == null || EntityHeader.IsNullOrEmpty(request.AgentContext))
            {
                const string msg = "AgentContext is required.";
                _adminLogger.AddError("[AgentExecutionService_ExecuteAsync__ValidateRequest]", msg);
                return InvokeResult<AgentExecuteResponse>.FromError(msg);
            }

            if (String.IsNullOrWhiteSpace(request.Mode))
            {
                const string msg = "Mode is required (e.g. 'ask' or 'edit').";
                _adminLogger.AddError("[AgentExecutionService_ExecuteAsync__ValidateRequest]", msg);
                return InvokeResult<AgentExecuteResponse>.FromError(msg);
            }

            var mode = request.Mode.Trim().ToLowerInvariant();

            _adminLogger.Trace(
                $"[AgentExecutionService_ExecuteAsync] Routing mode. CorrelationId={correlationId}, Mode={mode}");

            switch (mode)
            {
                case "ask":
                    return await HandleAskAsync(request, org, user, correlationId, cancellationToken);

                case "edit":
                    return HandleEditNotImplemented(correlationId);

                default:
                    var errorMsg = $"Unsupported mode '{request.Mode}'.";
                    _adminLogger.AddError("[AgentExecutionService_ExecuteAsync__UnsupportedMode]",
                        $"{errorMsg} CorrelationId={correlationId}");
                    return InvokeResult<AgentExecuteResponse>.FromError(errorMsg);
            }
        }

        private async Task<InvokeResult<AgentExecuteResponse>> HandleAskAsync(AgentExecuteRequest request,
            EntityHeader org, EntityHeader user, string correlationId,
            CancellationToken cancellationToken)
        {
            _adminLogger.Trace(
                $"[AgentExecutionService_ExecuteAsync__HandleAsk] Loading AgentContext. " +
                $"CorrelationId={correlationId}, AgentContextId={request.AgentContext.Id}");

            var agentContext = await _agentContextManager.GetAgentContextWithSecretsAsync(
                request.AgentContext.Id, org, user);

            _adminLogger.Trace(
                $"[AgentExecutionService_ExecuteAsync__SelectBehavior] Resolving ConversationContext. " +
                $"CorrelationId={correlationId}");

            var conversationContextId = string.Empty;
            if (request.ConversationContext != null && !EntityHeader.IsNullOrEmpty(request.ConversationContext))
            {
                conversationContextId = request.ConversationContext.Id;
            }
            else if (!EntityHeader.IsNullOrEmpty(agentContext.DefaultConversationContext))
            {
                conversationContextId = agentContext.DefaultConversationContext.Id;
            }

            _adminLogger.Trace(
                $"[AgentExecutionService_ExecuteAsync__ConversationId] Resolving ConversationId. " +
                $"CorrelationId={correlationId}, RequestConversationId={request.ConversationId}");

            var conversationId = String.IsNullOrWhiteSpace(request.ConversationId)
                ? Guid.NewGuid().ToId()
                : request.ConversationId;

            _adminLogger.Trace(
                $"[AgentExecutionService_ExecuteAsync__RAG] Invoking RAG pipeline. CorrelationId={correlationId}, " +
                $"ConversationId={conversationId}, Repo={request.Repo}, Language={request.Language ?? "csharp"}");

            var answerResult = await _ragAnswerService.AnswerAsync(agentContext.Id, request.Instruction,
                conversationContextId, org, user, request.Repo, request.Language ?? "csharp");

            if (!answerResult.Successful)
            {
                var errorMsg = answerResult.ErrorMessage ?? "Unknown RAG error.";
                _adminLogger.AddError("[AgentExecutionService_ExecuteAsync__RAGError]",
                    $"RAG pipeline failed. CorrelationId={correlationId}, Error={errorMsg}");

                return InvokeResult<AgentExecuteResponse>.FromError(errorMsg);
            }

            _adminLogger.Trace(
                $"[AgentExecutionService_ExecuteAsync__BuildResponse] Building answer response. " +
                $"CorrelationId={correlationId}, ConversationId={conversationId}");

            var response = new AgentExecuteResponse
            {
                Kind = "answer",
                ConversationId = conversationId,
                AgentContextId = agentContext.Id,
                ConversationContextId = conversationContextId,
                Mode = "ask",
                Text = answerResult.Result.Text,
                Sources = answerResult.Result.Sources
            };

            _adminLogger.Trace(
                $"[AgentExecutionService_ExecuteAsync__HandleAsk] Completed successfully. " +
                $"CorrelationId={correlationId}, ConversationId={conversationId}");

            return InvokeResult<AgentExecuteResponse>.Create(response);
        }

        private InvokeResult<AgentExecuteResponse> HandleEditNotImplemented(string correlationId)
        {
            const string msg = "Edit mode is not implemented yet.";

            _adminLogger.AddError("[AgentExecutionService_ExecuteAsync__EditNotImplemented]",
                $"{msg} CorrelationId={correlationId}");

            var response = new AgentExecuteResponse
            {
                Kind = "error",
                ErrorCode = "NOT_IMPLEMENTED",
                ErrorMessage = msg
            };

            return InvokeResult<AgentExecuteResponse>.Create(response);
        }
    }
}
