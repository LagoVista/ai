using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Interfaces.Services;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Pipeline
{
    /// <summary>
    /// AGN-032 Step: ContextProviderInitializer
    ///
    /// Expects:
    /// - <see cref="AgentPipelineContext.Session"/>, <see cref="AgentPipelineContext.ThisTurn"/>, and <see cref="AgentPipelineContext.Request"/> are present.
    /// - <see cref="AgentPipelineContext.AgentContext"/> and (optionally) <see cref="AgentPipelineContext.Role"/> are resolved.
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
    public sealed class PromptKnowledgeProviderInitializerPipelineStep : PipelineStep, IPromptKnowledgeProviderInitializerPipelineStep
    {
        private readonly IPromptKnowledgeProvider _pkpService;

        public PromptKnowledgeProviderInitializerPipelineStep(
            IAgentReasonerPipelineStep next,
            IAgentPipelineContextValidator validator,
            IPromptKnowledgeProvider pkpService,
            IAdminLogger adminLogger) : base(next, validator, adminLogger)
        {
            _pkpService = pkpService?? throw new ArgumentNullException(nameof(pkpService));
        }

        protected override PipelineSteps StepType => PipelineSteps.PromptKnowledgeProviderInitializer;

        protected override async Task<InvokeResult<IAgentPipelineContext>> ExecuteStepAsync(IAgentPipelineContext ctx)
        {
            return await _pkpService.PopulateAsync(ctx, false);
        }
 
    }
}
