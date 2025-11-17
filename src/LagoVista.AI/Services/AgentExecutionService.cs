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

        public AgentExecutionService(IAgentContextManager agentContextManager, IRagAnswerService ragAnswerService, IAdminLogger adminLogger)
        {
            _agentContextManager = agentContextManager ?? throw new ArgumentNullException(nameof(agentContextManager));
            _ragAnswerService = ragAnswerService ?? throw new ArgumentNullException(nameof(ragAnswerService));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task<InvokeResult<AgentExecuteResponse>> ExecuteAsync(AgentExecuteRequest request, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToId();

            _adminLogger.Trace(
                $"[AgentExecutionService_ExecuteAsync] Starting execution. " +
                $"correlationId={correlationId}, org={org?.Id}, user={user?.Id}, " +
                $"mode={request?.Mode}, agentContextId={request?.AgentContext?.Id}");

            if (request == null)
            {
                const string msg = "AgentExecuteRequest cannot be null.";
                _adminLogger.AddError("[AgentExecutionService_ExecuteAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_EXEC_NULL_REQUEST");
            }

            if (request.AgentContext == null || EntityHeader.IsNullOrEmpty(request.AgentContext))
            {
                const string msg = "AgentContext is required.";
                _adminLogger.AddError("[AgentExecutionService_ExecuteAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_EXEC_MISSING_AGENT_CONTEXT");
            }

            if (String.IsNullOrWhiteSpace(request.Mode))
            {
                const string msg = "Mode is required (e.g. 'ask' or 'edit').";
                _adminLogger.AddError("[AgentExecutionService_ExecuteAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_EXEC_MISSING_MODE");
            }

            if (String.IsNullOrWhiteSpace(request.Instruction))
            {
                const string msg = "Instruction is required.";
                _adminLogger.AddError("[AgentExecutionService_ExecuteAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_EXEC_MISSING_INSTRUCTION");
            }

            var mode = request.Mode.Trim().ToLowerInvariant();

            _adminLogger.Trace($"[AgentExecutionService_ExecuteAsync] Routing mode. correlationId={correlationId}, mode={mode}");

            switch (mode)
            {
                case "ask":
                    return await HandleAskAsync(request, org, user, correlationId, cancellationToken);

                case "edit":
                    return HandleEditNotImplemented(correlationId);

                default:
                    var errorMsg = $"Unsupported mode '{request.Mode}'.";
                    _adminLogger.AddError("[AgentExecutionService_ExecuteAsync__UnsupportedMode]", $"{errorMsg} correlationId={correlationId}");

                    return InvokeResult<AgentExecuteResponse>.FromError(errorMsg, "AGENT_EXEC_UNSUPPORTED_MODE");
            }
        }

        private async Task<InvokeResult<AgentExecuteResponse>> HandleAskAsync(AgentExecuteRequest request, EntityHeader org, EntityHeader user, string correlationId, CancellationToken cancellationToken)
        {
            _adminLogger.Trace(
                $"[AgentExecutionService_ExecuteAsync__HandleAsk] Loading AgentContext. " +
                $"correlationId={correlationId}, agentContextId={request.AgentContext.Id}");

            var agentContext = await _agentContextManager.GetAgentContextWithSecretsAsync(request.AgentContext.Id, org, user);

            _adminLogger.Trace($"[AgentExecutionService_ExecuteAsync__SelectBehavior] Resolving ConversationContext. correlationId={correlationId}");

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
                $"correlationId={correlationId}, requestConversationId={request.ConversationId}");

            var conversationId = String.IsNullOrWhiteSpace(request.ConversationId) ? Guid.NewGuid().ToId() : request.ConversationId;

            _adminLogger.Trace(
                $"[AgentExecutionService_ExecuteAsync__RAG] Invoking RAG pipeline. " +
                $"correlationId={correlationId}, conversationId={conversationId}, " +
                $"repo={request.Repo}, language={request.Language ?? "csharp"}");

            var answerResult = await _ragAnswerService.AnswerAsync(
                agentContext.Id,
                request.Instruction,
                conversationContextId,
                org,
                user,
                repo: request.Repo,
                language: request.Language ?? "csharp",
                topK: 8,
                ragScope: request.RagScope,
                workspaceId: request.WorkspaceId,
                activeFiles: request.ActiveFiles);

            if (!answerResult.Successful)
            {
                _adminLogger.AddError("[AgentExecutionService_ExecuteAsync__RAGError]", "RAG pipeline failed.", answerResult.ErrorsToKVPArray());

                return InvokeResult<AgentExecuteResponse>.FromInvokeResult(answerResult.ToInvokeResult());
            }

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

            return InvokeResult<AgentExecuteResponse>.Create(response);
        }

        private InvokeResult<AgentExecuteResponse> HandleEditNotImplemented(string correlationId)
        {
            const string msg = "Edit mode is not implemented yet.";

            _adminLogger.AddError("[AgentExecutionService_ExecuteAsync__EditNotImplemented]", $"{msg} correlationId={correlationId}");

            var response = new AgentExecuteResponse
            {
                Kind = "error",
                ErrorCode = "AGENT_EXEC_EDIT_NOT_IMPLEMENTED",
                ErrorMessage = msg
            };

            return InvokeResult<AgentExecuteResponse>.Create(response);
        }
    }
}
