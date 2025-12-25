using System;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services.Pipeline
{
    /// <summary>
    /// AGN-032 Step: ContextProviderInitializer
    ///
    /// Expects:
    /// - <see cref="AgentPipelineContext.Session"/>, <see cref="AgentPipelineContext.Turn"/>, and <see cref="AgentPipelineContext.Request"/> are present.
    /// - <see cref="AgentPipelineContext.AgentContext"/> and (optionally) <see cref="AgentPipelineContext.ConversationContext"/> are resolved.
    ///
    /// Updates:
    /// - Initializes session/mode-based context providers required by downstream execution (RAG, tool catalogs, prompt inputs, etc.).
    /// - Does not produce a response; it prepares state for downstream steps.
    ///
    /// Branching:
    /// - If <see cref="AgentPipelineContext.Type"/> is <see cref="AgentPipelineContextTypes.ClientToolCallContinuation"/>,
    ///   route to <see cref="IClientToolContinuationResolverPipelineStep"/>.
    /// - Otherwise (Initial / FollowOn), route to <see cref="IAgentReasonerPipelineStep"/>.
    ///
    /// Next:
    /// - ClientToolContinuationResolver OR AgentReasoner
    /// </summary>
    public sealed class ContextProviderInitializerPipelineStep : IContextProviderInitializerPipelineStep
    {
        private readonly IClientToolContinuationResolverPipelineStep _clientToolContinuationResolver;
        private readonly IAgentReasonerPipelineStep _agentReasoner;
        private readonly IAdminLogger _adminLogger;

        public ContextProviderInitializerPipelineStep(
            IClientToolContinuationResolverPipelineStep clientToolContinuationResolver,
            IAgentReasonerPipelineStep agentReasoner,
            IAdminLogger adminLogger)
        {
            _clientToolContinuationResolver = clientToolContinuationResolver ?? throw new ArgumentNullException(nameof(clientToolContinuationResolver));
            _agentReasoner = agentReasoner ?? throw new ArgumentNullException(nameof(agentReasoner));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx)
        {
            var validation = AgentPipelineContext.ValidateInputs(ctx, PipelineSteps.ContextProviderInitializer);


            _adminLogger.Trace("[ContextProviderInitializerPipelineStep__ExecuteAsync] - Initializing context providers.",
                (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                (ctx.Org?.Id ?? string.Empty).ToKVP("TenantId"),
                (ctx.User?.Id ?? string.Empty).ToKVP("UserId"),
                (ctx.Session?.Id ?? string.Empty).ToKVP("SessionId"),
                (ctx.Turn?.Id ?? string.Empty).ToKVP("TurnId"),
                (ctx.Type.ToString()).ToKVP("PipelineContextType"));

            // Stub: "meat" (provider initialization) will be added later.

            if (ctx.CancellationToken.IsCancellationRequested)
            {
                return InvokeResult<AgentPipelineContext>.Abort();
            }

            if (ctx.Type == AgentPipelineContextTypes.ClientToolCallContinuation)
            {
                _adminLogger.Trace("[ContextProviderInitializerPipelineStep__ExecuteAsync] - Routing: client tool continuation resolver.",
                    (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                    (ctx.Session?.Id ?? string.Empty).ToKVP("SessionId"),
                    (ctx.Turn?.Id ?? string.Empty).ToKVP("TurnId"));

                return await _clientToolContinuationResolver.ExecuteAsync(ctx);
            }

            _adminLogger.Trace("[ContextProviderInitializerPipelineStep__ExecuteAsync] - Routing: agent reasoner.",
                (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                (ctx.Session?.Id ?? string.Empty).ToKVP("SessionId"),
                (ctx.Turn?.Id ?? string.Empty).ToKVP("TurnId"));

            return await _agentReasoner.ExecuteAsync(ctx);
        }
    }
}
