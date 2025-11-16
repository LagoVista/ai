using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
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
            _agentContextManager = agentContextManager ?? throw new ArgumentNullException(nameof(agentContextManager));
            _ragAnswerService = ragAnswerService ?? throw new ArgumentNullException(nameof(ragAnswerService));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task<InvokeResult<AgentExecuteResponse>> ExecuteAsync(AgentExecuteRequest request,
            EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return InvokeResult<AgentExecuteResponse>.FromError("AgentExecuteRequest cannot be null.");
            }

            if (request.AgentContext == null || EntityHeader.IsNullOrEmpty(request.AgentContext))
            {
                return InvokeResult<AgentExecuteResponse>.FromError("AgentContext is required.");
            }

            if (String.IsNullOrWhiteSpace(request.Mode))
            {
                return InvokeResult<AgentExecuteResponse>.FromError("Mode is required (e.g. 'ask' or 'edit').");
            }

            var mode = request.Mode.Trim().ToLowerInvariant();

            switch (mode)
            {
                case "ask":
                    return await HandleAskAsync(request, org, user, cancellationToken);

                case "edit":
                    return HandleEditNotImplemented();

                default:
                    _adminLogger.AddError("AgentExecutionService_ExecuteAsync",
                        $"Unsupported mode '{request.Mode}' in AgentExecuteRequest.");
                    return InvokeResult<AgentExecuteResponse>.FromError($"Unsupported mode '{request.Mode}'.");
            }
        }

        private async Task<InvokeResult<AgentExecuteResponse>> HandleAskAsync(AgentExecuteRequest request,
            EntityHeader org, EntityHeader user, CancellationToken cancellationToken)
        {
            // Load the agent context (Agent Profile) with secrets.
            var agentContext = await _agentContextManager.GetAgentContextWithSecretsAsync(request.AgentContext.Id,
                org, user);

            // Resolve conversation context (Behavior Profile).
            var conversationContextId = string.Empty;
            if (request.ConversationContext != null && !EntityHeader.IsNullOrEmpty(request.ConversationContext))
            {
                conversationContextId = request.ConversationContext.Id;
            }
            else if (!EntityHeader.IsNullOrEmpty(agentContext.DefaultConversationContext))
            {
                conversationContextId = agentContext.DefaultConversationContext.Id;
            }

            // TODO: Hook real conversation store here; for now, just ensure we have an ID.
            var conversationId = String.IsNullOrWhiteSpace(request.ConversationId)
                ? Guid.NewGuid().ToString()
                : request.ConversationId;

            // Delegate to existing RAG pipeline for answer mode.
            var answerResult = await _ragAnswerService.AnswerAsync(agentContext.Id, request.Instruction,
                conversationContextId, org, user, request.Repo, request.Language ?? "csharp");

            if (!answerResult.Successful)
            {
                return InvokeResult<AgentExecuteResponse>.FromError(answerResult.ErrorMessage);
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

        private InvokeResult<AgentExecuteResponse> HandleEditNotImplemented()
        {
            var response = new AgentExecuteResponse
            {
                Kind = "error",
                ErrorCode = "NOT_IMPLEMENTED",
                ErrorMessage = "Edit mode is not implemented yet."
            };

            return InvokeResult<AgentExecuteResponse>.Create(response);
        }
    }
}
