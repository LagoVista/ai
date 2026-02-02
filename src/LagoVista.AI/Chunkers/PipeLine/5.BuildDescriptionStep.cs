using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.PipeLine
{
    public sealed class BuildDescriptionStep : IndexingPipelineStepBase, IBuildDescriptionStep
    {
        IBuildDescriptionProcessorRegistry _registry;

        public BuildDescriptionStep(IBuildDescriptionProcessorRegistry buildDescriptionsRegistry, IUploadContentStep next) : base(next) 
        {
            _registry = buildDescriptionsRegistry ?? throw new System.ArgumentNullException(nameof(buildDescriptionsRegistry));
        }

        public override IndexingPipelineSteps StepType => IndexingPipelineSteps.BuildDescription;

        protected override async Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            if (_registry.TryGet(workItem.Kind, out IBuildDescriptionProcessor processor))
            {
                var result = await processor.ProcessAsync(ctx, workItem);

                if (!result.Successful) return result.ToInvokeResult();
                workItem.RagPayload.Meta.Subtype = workItem.Kind.ToString();
                workItem.Lenses.EmbedSnippet = result.Result.BuildSummaryForEmbedding();
                workItem.Lenses.ModelSummary = result.Result.BuildSummaryForModel();
                workItem.Lenses.UserDetail = result.Result.BuildSummaryForHuman();
            }

            return InvokeResult.Success;
        }
    }
}
