using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.PipeLine
{
    public sealed class SegmentContentStep : IndexingPipelineStepBase
    {
        public SegmentContentStep(LagoVista.AI.Indexing.Interfaces.IIndexingPipelineStep next = null) : base(next) { }

        public override IndexingPipelineSteps StepType => IndexingPipelineSteps.SegmentContent;

        protected override Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            // TODO:
            // - Never replace original work item
            // - Add child work items via ctx.CloneAndAddChild(parent) (deep copy semantics)
            // - Set ParentPointId on payload (once payload is strongly typed)
            // - Populate EmbedSnippet for each segment
            return Task.FromResult(InvokeResult.Success);
        }
    }
}
