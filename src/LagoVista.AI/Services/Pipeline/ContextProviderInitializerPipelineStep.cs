using System;
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
    public sealed class ContextProviderInitializerPipelineStep : PipelineStep, IContextProviderInitializerPipelineStep
    {
        private readonly IAdminLogger _adminLogger;

        public ContextProviderInitializerPipelineStep(
            IAgentReasonerPipelineStep next,
            IAdminLogger adminLogger) : base(next, adminLogger)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        protected override PipelineSteps StepType => PipelineSteps.PromptContentProvider;

        protected override async Task<InvokeResult<IAgentPipelineContext>> ExecuteStepAsync(IAgentPipelineContext ctx)
        {
            return InvokeResult<IAgentPipelineContext>.Create(ctx);
        }
    }
}
