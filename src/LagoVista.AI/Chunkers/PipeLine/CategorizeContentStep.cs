using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.PipeLine
{
    public sealed class CategorizeContentStep : IndexingPipelineStepBase
    {
        public CategorizeContentStep(LagoVista.AI.Indexing.Interfaces.IIndexingPipelineStep next = null) : base(next) { }

        public override IndexingPipelineSteps StepType => IndexingPipelineSteps.CategorizeContent;

        protected override Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            // TODO:
            // - Set RagPayload.Meta.ContentTypeId = SourceCode
            // - Derive RagPayload.Meta.Subtype from symbol text + file path
            return Task.FromResult(InvokeResult.Success);
        }
    }
}
