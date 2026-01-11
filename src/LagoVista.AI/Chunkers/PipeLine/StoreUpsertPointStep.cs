using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.PipeLine
{
    public sealed class StoreUpsertPointStep : IndexingPipelineStepBase
    {
        public StoreUpsertPointStep(LagoVista.AI.Indexing.Interfaces.IIndexingPipelineStep next = null) : base(next) { }

        public override IndexingPipelineSteps StepType => IndexingPipelineSteps.StoreUpsertPoint;

        protected override Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            // TODO:
            // - Upsert point into vector DB: PointId + Vector + RagPayload
            // - Vendor-agnostic; integration may remove prior points by DocId
            return Task.FromResult(InvokeResult.Success);
        }
    }
}
