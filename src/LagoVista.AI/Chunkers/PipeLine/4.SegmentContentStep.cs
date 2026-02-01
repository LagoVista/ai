using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.PipeLine
{
    public sealed class SegmentContentStep : IndexingPipelineStepBase, ISegmentContentStep
    {
        private readonly ISegmentContentProcessorRegistry _registry;

        public SegmentContentStep(ISegmentContentProcessorRegistry registry, IBuildDescriptionStep next) : base(next) 
        { 
            _registry = registry ?? throw new System.ArgumentNullException(nameof(registry));
        }

        public override IndexingPipelineSteps StepType => IndexingPipelineSteps.SegmentContent;

        protected override async Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            // Perhaps in the future we may want to slice up a symbol into smaller chunks.
            //if(_registry.TryGet(workItem.Kind, out ISegmentContentProcessor processor))
            //{
            //   var result = await  processor.ProcessAsync(ctx, workItem);
            //    if (!result.Successful) return result;
            //}
       
            return InvokeResult.Success;
        }
    }
}
