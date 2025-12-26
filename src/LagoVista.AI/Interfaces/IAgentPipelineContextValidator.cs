using LagoVista.AI.Models;
using LagoVista.AI.Models.Context;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentPipelineContextValidator
    {
        /// <summary>
        /// Validates the core object after it's initially created.
        /// This should be safe to call multiple times; it must not mutate ctx.
        /// </summary>
        InvokeResult ValidateCore(IAgentPipelineContext ctx);

        /// <summary>
        /// To be called prior to entering a pipeline step to ensure
        /// it has what it needs.
        /// </summary>
        /// <param name="ctx">Pipeline context.</param>
        /// <param name="step">Step that WILL be executed.</param>
        InvokeResult ValidatePreStep(IAgentPipelineContext ctx, PipelineSteps step);

        /// <summary>
        /// To be called after a pipeline step completes to ensure
        /// required outputs / invariants are present.
        /// </summary>
        /// <param name="ctx">Pipeline context.</param>
        /// <param name="step">Step that WAS just executed.</param>
        InvokeResult ValidatePostStep(IAgentPipelineContext ctx, PipelineSteps step);

        /// <summary>
        /// ToolCallManifest — Validity Definition (LOCKED)
        /// - ToolCalls and ToolCallResults must have same count.
        /// - Order must match.
        /// - ToolCallId and Name must match pairwise.
        /// - All ToolCallResults MUST have ResultJson.
        /// </summary>
        InvokeResult ValidateToolCallManifest(ToolCallManifest manifest);
    }
}
