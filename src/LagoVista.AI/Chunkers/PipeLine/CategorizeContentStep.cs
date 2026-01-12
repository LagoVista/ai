using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.PipeLine
{
    public sealed class CategorizeContentStep : IndexingPipelineStepBase, ICategorizeContentStep
    {
        ISubtypeKindCategorizerRegistry _registry;

        public CategorizeContentStep(ISegmentContentStep next, ISubtypeKindCategorizerRegistry registry) : base(next) {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public override IndexingPipelineSteps StepType => IndexingPipelineSteps.CategorizeContent;

        protected override async Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            if(_registry.TryGet(ctx.Resources.FileContext.DocumentIdentity.Type, out ISubtypeKindCategorizer categorizer))
            {
                var result = await categorizer.ProcessAsync(ctx, workItem);
                if (!result.Successful) return result;
            }

            // if we don't have one...don't bother attempting to populate the subtype kind.
            return InvokeResult.Success;
        }
    }
}
