using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Services.Tools;
using LagoVista.Core;
using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// High-level orchestration entry point for executing an agent request.
    ///
    /// Responsibilities:
    /// - Validate the incoming AgentExecuteRequest.
    /// - Load the AgentContext (with secrets) for the specified AgentContext header.
    /// - Resolve the effective ConversationContext for this request.
    /// - Attach the mode catalog system prompt for the current mode.
    /// - Invoke the RAG pipeline to build the ragContextBlock.
    /// - Delegate to IAgentReasoner for the actual LLM/tool loop.
    ///
    /// This service is intentionally mode-agnostic: concrete behavior is driven
    /// by the mode-specific system prompts, tool catalog, and downstream reasoner.
    /// </summary>
    public class AgentExecutionService : IAgentExecutionService
    {
        private const string DefaultMode = "general";

        private readonly IAgentContextManager _agentContextManager;
        private readonly IAdminLogger _adminLogger;
        private readonly IAgentReasoner _reasoner;
        private readonly IRagContextBuilder _ragContextBuilder;

        public AgentExecutionService(
            IAgentContextManager agentContextManager,
            IAgentReasoner agentReasoner,
            IRagContextBuilder ragContextBuilder,
            IAdminLogger adminLogger)
        {
            _agentContextManager = agentContextManager ?? throw new ArgumentNullException(nameof(agentContextManager));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _reasoner = agentReasoner ?? throw new ArgumentNullException(nameof(agentReasoner));
            _ragContextBuilder = ragContextBuilder ?? throw new ArgumentNullException(nameof(ragContextBuilder));
        }

        public async Task<InvokeResult<AgentExecuteResponse>> ExecuteAsync(
            AgentExecuteRequest request,
            EntityHeader org,
            EntityHeader user,
            CancellationToken cancellationToken = default)
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

            if (string.IsNullOrWhiteSpace(request.Instruction))
            {
                const string msg = "Instruction is required.";
                _adminLogger.AddError("[AgentExecutionService_ExecuteAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_EXEC_MISSING_INSTRUCTION");
            }

            //
            // Mode normalization (AGN-011 / AGN-013)
            //
            if (string.IsNullOrWhiteSpace(request.Mode))
            {
                // Default to "general" if not provided. Upstream callers are
                // expected to seed sessions with general, but we guard here too.
                _adminLogger.AddError(
                    "[AgentExecutionService_ExecuteAsync__MissingMode]",
                    "Mode was null or whitespace; defaulting to 'general'.");

                request.Mode = DefaultMode;
            }

            // Normalize case/whitespace for downstream components.
            var modeKey = request.Mode.Trim().ToLowerInvariant();
            request.Mode = modeKey;

            _adminLogger.Trace(
                $"[AgentExecutionService_ExecuteAsync] Using mode='{modeKey}'. " +
                $"correlationId={correlationId}");

            // For now, all modes follow the same execution path; concrete differences
            // come from system prompts, RAG scope, and mode-specific tools.
            return await ExecuteWithRagAndReasonerAsync(
                request,
                modeKey,
                org,
                user,
                correlationId,
                cancellationToken);
        }

        private async Task<InvokeResult<AgentExecuteResponse>> ExecuteWithRagAndReasonerAsync(AgentExecuteRequest request, string modeKey, EntityHeader org, 
            EntityHeader user, string correlationId, CancellationToken cancellationToken)
        {
            _adminLogger.Trace(
                $"[AgentExecutionService_ExecuteAsync__LoadAgentContext] Loading AgentContext. " +
                $"correlationId={correlationId}, agentContextId={request.AgentContext.Id}");

            var agentContext = await _agentContextManager.GetAgentContextWithSecretsAsync(request.AgentContext.Id, org, user);
            _adminLogger.Trace( $"[AgentExecutionService_ExecuteAsync__SelectConversationContext] Resolving ConversationContext. " +
                $"correlationId={correlationId}");

            var conversationContextId = string.Empty;
            if (request.ConversationContext != null && !EntityHeader.IsNullOrEmpty(request.ConversationContext))
            {
                conversationContextId = request.ConversationContext.Id;
            }
            else if (!EntityHeader.IsNullOrEmpty(agentContext.DefaultConversationContext))
            {
                conversationContextId = agentContext.DefaultConversationContext.Id;
            }

            if (string.IsNullOrWhiteSpace(conversationContextId) && agentContext.ConversationContexts.Any())
            {
                conversationContextId = agentContext.ConversationContexts.First().Id;
            }

            if (string.IsNullOrWhiteSpace(conversationContextId))
            { 
                const string msg = "Unable to resolve ConversationContext for the request.";
                _adminLogger.AddError("[AgentExecutionService_ExecuteAsync__MissingConversationContext]", $"{msg} correlationId={correlationId}");

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_EXEC_MISSING_CONVERSATION_CONTEXT");
            }

            var conversationContext = agentContext.ConversationContexts.Single(ctx => ctx.Id == conversationContextId);

            _adminLogger.Trace($"[AgentExecutionService_ExecuteAsync__ConversationId] Resolving ConversationId. " +
                $"correlationId={correlationId}, requestConversationId={request.ConversationId}");

            var conversationId = string.IsNullOrWhiteSpace(request.ConversationId) ? Guid.NewGuid().ToId() : request.ConversationId;

            // Mode catalog system prompt (AGN-013)
            var modeCatalogSystemPrompt = agentContext.BuildSystemPrompt(modeKey);
            if (!string.IsNullOrEmpty(modeCatalogSystemPrompt))
            {
                if (conversationContext.SystemPrompts == null)
                {
                    conversationContext.SystemPrompts = new List<string>();
                }

                conversationContext.SystemPrompts.Add(modeCatalogSystemPrompt);
            }

            _adminLogger.Trace( $"[AgentExecutionService_ExecuteAsync__RAG] Invoking RAG pipeline. " +
                $"correlationId={correlationId}, conversationId={conversationId}, " +
                $"repo={request.Repo}, language={request.Language ?? "csharp"}");

            //var ragContextBlock = await _ragContextBuilder.BuildContextSectionAsync(
            //    agentContext,
            //    request.Instruction,
            //    request.RagScopeFilter);

            //if (!ragContextBlock.Successful)
            //{
            //    return InvokeResult<AgentExecuteResponse>.FromInvokeResult(
            //        ragContextBlock.ToInvokeResult());
            //}

            var ragContextBlock = InvokeResult<string>.Create("");

            // correlationId is used as the sessionId for the Reasoner; if/when you
            // introduce a formal AgentSession id here, you can swap it in.
            return await _reasoner.ExecuteAsync(
                agentContext,
                conversationContext,
                request,
                ragContextBlock.Result,
                conversationId,
                org,
                user,
                cancellationToken);
        }

    }
}
