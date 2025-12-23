using System;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services.Pipeline
{
    /// <summary>
    /// AGN-032 Step: SessionContextResolver
    ///
    /// Expects:
    /// - <see cref="AgentPipelineContext.Session"/> and <see cref="AgentPipelineContext.Turn"/> are present.
    /// - <see cref="AgentPipelineContext.Request"/> is present.
    /// - <see cref="AgentPipelineContext.Org"/> and <see cref="AgentPipelineContext.User"/> identities are present.
    ///
    /// Updates:
    /// - Loads and assigns <see cref="AgentPipelineContext.AgentContext"/> for the active session.
    /// - Loads and assigns <see cref="AgentPipelineContext.ConversationContext"/> when specified by request/session.
    /// - Ensures the effective mode catalog state for the turn is valid (e.g., guarantees the default "general" mode exists).
    ///   Any required mode normalization is persisted via the context manager when needed.
    ///
    /// Notes:
    /// - This step is the canonical place to translate session/request identity into the concrete runtime contexts consumed by
    ///   downstream providers (RAG, tools, prompt builders, etc.).
    /// - Session creation/restoration steps are responsible for establishing Session/Turn; this step is responsible for
    ///   resolving the contexts those steps point to.
    ///
    /// Next:
    /// - <c>ContextProviderInitializer</c> (<see cref="IContextProviderInitializerPipelineStep"/>).
    /// </summary>
    public sealed class SessionContextResolverPipelineStep : ISessionContextResolverPipelineStep
    {
        private readonly IContextProviderInitializerPipelineStep _next;
        private readonly IAgentContextManager _contextManager;
        private readonly IAdminLogger _adminLogger;

        public SessionContextResolverPipelineStep(
            IContextProviderInitializerPipelineStep next,
            IAgentContextManager contextManager,
            IAdminLogger adminLogger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx)
        {
            if (ctx == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "AgentPipelineContext cannot be null.",
                    "SESSION_CTX_RESOLVER_NULL_CONTEXT");
            }

            if (ctx.Request == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "AgentExecuteRequest is required.",
                    "SESSION_CTX_RESOLVER_MISSING_REQUEST");
            }

            if (ctx.Org == null || EntityHeader.IsNullOrEmpty(ctx.Org))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "Org is required.",
                    "SESSION_CTX_RESOLVER_MISSING_ORG");
            }

            if (ctx.User == null || EntityHeader.IsNullOrEmpty(ctx.User))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "User is required.",
                    "SESSION_CTX_RESOLVER_MISSING_USER");
            }

            if (ctx.Session == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "Session is required.",
                    "SESSION_CTX_RESOLVER_MISSING_SESSION");
            }

            if (ctx.Turn == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "Turn is required.",
                    "SESSION_CTX_RESOLVER_MISSING_TURN");
            }

            _adminLogger.Trace("[SessionContextResolverPipelineStep__ExecuteAsync] - Resolving AgentContext and ConversationContext.",
                (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                (ctx.Org?.Id ?? string.Empty).ToKVP("TenantId"),
                (ctx.User?.Id ?? string.Empty).ToKVP("UserId"),
                (ctx.Session?.Id ?? string.Empty).ToKVP("SessionId"),
                (ctx.Turn?.Id ?? string.Empty).ToKVP("TurnId"),
                (ctx.Request?.Mode ?? string.Empty).ToKVP("Mode"));

            // 1) Resolve AgentContext: prefer Session.AgentContext, fall back to Request.AgentContext.
            var agentContextId = ctx.Session?.AgentContext?.Id ?? ctx.Request.AgentContext?.Id;
            if (string.IsNullOrWhiteSpace(agentContextId))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "AgentContextId is required.",
                    "SESSION_CTX_RESOLVER_MISSING_AGENT_CONTEXT_ID");
            }

            // NOTE: In the old orchestrator:
            // - new session used GetAgentContextWithSecretsAsync(...)
            // - follow-on used GetAgentContextAsync(...)
            // AGN-032 doesn't specify the secrets policy at this step, so we choose the safer default: non-secrets.
            // If you want secrets here, swap to GetAgentContextWithSecretsAsync and/or branch by request/session.
            var agentContext = await _contextManager.GetAgentContextAsync(agentContextId, ctx.Org, ctx.User);
            if (agentContext == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "AgentContext not found.",
                    "SESSION_CTX_RESOLVER_AGENT_CONTEXT_NOT_FOUND");
            }

            // Ensure "general" exists as a baseline mode (mirrors orchestrator behavior).
            if (!agentContext.AgentModes.Any(mode => mode.Key == "general"))
            {
                var addGeneral = await EnsureGeneralModeAsync(agentContext, ctx.Org, ctx.User);
                if (!addGeneral.Successful)
                {
                    return addGeneral;
                }
            }

            ctx.AgentContext = agentContext;

            // 2) Resolve ConversationContext (optional): prefer Request.ConversationContext, else ctx.Session?.ConversationContext if present.
            // (We don't have Session.ConversationContext in the snippets provided; this keeps logic request-first.)
            var conversationContextId = ctx.Request.ConversationContext?.Id ?? agentContext.DefaultConversationContext?.Id;
            if(String.IsNullOrEmpty(conversationContextId) && agentContext.ConversationContexts.Any())
            {
                conversationContextId = agentContext.ConversationContexts.First().Id;
            }

            if (!string.IsNullOrWhiteSpace(conversationContextId))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                "ConversationContextId not found.",
                "SESSION_CTX_RESOLVER_CONVERSATION_CONTEXT_ID_NOT_AVAILABLE");
            }

            if (!string.IsNullOrWhiteSpace(conversationContextId))
            {
                var conversationContext = agentContext.ConversationContexts.FirstOrDefault(ctx => ctx.Id == conversationContextId); 
                if (conversationContext == null)
                {
                    return InvokeResult<AgentPipelineContext>.FromError(
                        "ConversationContext not found.",
                        "SESSION_CTX_RESOLVER_CONVERSATION_CONTEXT_NOT_FOUND");
                }

                ctx.ConversationContext = conversationContext;
            }

            if (ctx.CancellationToken.IsCancellationRequested)
            {
                return InvokeResult<AgentPipelineContext>.Abort();
            }

            _adminLogger.Trace("[SessionContextResolverPipelineStep__ExecuteAsync] - Contexts resolved.",
                (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                (ctx.AgentContext?.Id ?? string.Empty).ToKVP("AgentContextId"),
                (ctx.ConversationContext?.Id ?? string.Empty).ToKVP("ConversationContextId"));

            return await _next.ExecuteAsync(ctx);
        }

        private async Task<InvokeResult<AgentPipelineContext>> EnsureGeneralModeAsync(AgentContext context, EntityHeader org, EntityHeader user)
        {
            try
            {
                var mode = new AgentMode
                {
                    Id = Guid.NewGuid().ToId(),
                    Key = "general",
                    DisplayName = "General Mode",
                    Description = "General-purpose assistance for everyday Q&A, explanation, and lightweight help.",
                    WhenToUse = "Use this mode for everyday Q&A, explanation, and lightweight assistance.",
                    WelcomeMessage = "You are now in General mode. Use this mode for broad questions and lightweight assistance",
                    ModeInstructionDdrs = new[]
                    {
                        "You are operating in General mode. Provide helpful and accurate responses to a wide range of user queries.",
                        "Focus on clarity and conciseness in your answers.",
                        "If you don't know the answer, admit it rather than making something up."
                    },
                    BehaviorHints = new[] { "preferConversationalTone" },
                    HumanRoleHints = new[] { "The human is seeking general information and assistance." },
                    AssociatedToolIds = new[] { "agent_hello_world", "agent_hello_world_client", "add_agent_mode", "update_agent_mode" },
                    ToolGroupHints = new[] { "general_tools", "workspace" },
                    RagScopeHints = Array.Empty<string>(),
                    StrongSignals = Array.Empty<string>(),
                    WeakSignals = Array.Empty<string>(),
                    ExampleUtterances = new[]
                    {
                        "Review this PR diff and suggest improvements.",
                        "Does this function handle edge cases?",
                        "Propose a minimal patch to fix naming and add a comment.",
                        "Flag any security issues in this handler."
                    },
                    Status = "active",
                    Version = "v1",
                    IsDefault = true
                };

                context.AgentModes.Add(mode);

                await _contextManager.UpdateAgentContextAsync(context, org, user);

                _adminLogger.Trace("[SessionContextResolverPipelineStep__EnsureGeneralModeAsync] - Added missing 'general' mode.",
                    (context?.Id ?? string.Empty).ToKVP("AgentContextId"));

                return InvokeResult<AgentPipelineContext>.Create(new AgentPipelineContext());
            }
            catch (Exception ex)
            {
                _adminLogger.AddException("[SessionContextResolverPipelineStep__EnsureGeneralModeAsync]", ex);
                return InvokeResult<AgentPipelineContext>.FromException("[SessionContextResolverPipelineStep__EnsureGeneralModeAsync]", ex);
            }
        }
    }
}
