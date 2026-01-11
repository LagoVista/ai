using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.PipeLine
{
    public sealed class BuildDescriptionStep : IndexingPipelineStepBase, IBuildDescriptionStep
    {
        public BuildDescriptionStep(LagoVista.AI.Indexing.Interfaces.IIndexingPipelineStep next = null) : base(next) { }

        public override IndexingPipelineSteps StepType => IndexingPipelineSteps.BuildDescription;

        protected override Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            // TODO:
            // - Populate lens fields (EmbedSnippet, ModelSummary, UserDetail)
            // - CleanupGuidance optional
            // - Populate additional RagPayload fields as needed
            return Task.FromResult(InvokeResult.Success);
        }
    }
}
