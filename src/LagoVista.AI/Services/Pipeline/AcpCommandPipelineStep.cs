using System;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services.Pipeline
{
    /// <summary>
    /// Pre-LLM pipeline step that attempts to match and execute an ACP command.
    /// If no match (or unbalanced quotes), it falls through to the next step (LLM path).
    /// If a match is found, the command must succeed 100% of the time; failures return immediately.
    /// </summary>
    public sealed class AcpCommandPipelineStep : PipelineStep, IAcpCommandPipelineStep
    {
        private readonly IAcpCommandRouter _router;
        private readonly IAcpCommandExecutor _executor;
        private readonly IAdminLogger _adminLogger;

        public AcpCommandPipelineStep(IPromptKnowledgeProviderInitializerPipelineStep next, IAcpCommandRouter router,
            IAcpCommandExecutor executor, IAgentPipelineContextValidator validator, IAdminLogger adminLogger) : base(next, validator, adminLogger)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        protected override PipelineSteps StepType => PipelineSteps.AcpCommandHandler;

        protected override async Task<InvokeResult<IAgentPipelineContext>> ExecuteStepAsync(IAgentPipelineContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            var inputText = ctx.Envelope?.Instructions;

            if (String.IsNullOrWhiteSpace(inputText))
                return InvokeResult<IAgentPipelineContext>.Create(ctx);

            var route = _router.Route(inputText);

            if (route.Outcome == AcpRouteOutcome.NoMatch)
            {
                _adminLogger.Trace($"{this.Tag()} No match returning context", inputText.ToKVP("inputText"));
                return InvokeResult<IAgentPipelineContext>.Create(ctx);
            }

            _adminLogger.Trace($"{this.Tag()} match: {route.CommandId}");

            if (route.Outcome == AcpRouteOutcome.MultipleMatch)
            {
                // v1: no picker yet => fail fast so you can fix overlapping triggers
                var ids = route.CandidateCommandIds == null ? "(none)" : String.Join(", ", route.CandidateCommandIds);

                return InvokeResult<IAgentPipelineContext>.FromError($"Multiple ACP commands matched input. Matched: {ids}","ACP_MULTIPLE_MATCH");
            }

            // Single match => execute (failures return immediately)
            if (!route.CanExecute)
                return InvokeResult<IAgentPipelineContext>.FromError("Router returned SingleMatch but route is not executable.", "ACP_ROUTER_INVALID_STATE");

           return await _executor.ExecuteAsync(route.CommandId, ctx, route.Args);
        }
    }
}