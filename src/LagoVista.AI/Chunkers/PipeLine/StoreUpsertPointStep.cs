using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.PipeLine
{
    public sealed class StoreUpsertPointStep : IndexingPipelineStepBase, IStoreUpsertPointStep
    {
        public StoreUpsertPointStep() : base() { }

        public override IndexingPipelineSteps StepType => IndexingPipelineSteps.StoreUpsertPoint;

        protected override Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            ctx.IsTerminal = true;

            // TODO:
            // - Upsert point into vector DB: PointId + Vector + RagPayload
            // - Vendor-agnostic; integration may remove prior points by DocId
            return Task.FromResult(InvokeResult.Success);
        }
    }
}
