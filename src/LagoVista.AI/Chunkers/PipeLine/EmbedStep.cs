using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.PipeLine
{
    public sealed class EmbedStep : IndexingPipelineStepBase
    {
        public EmbedStep(LagoVista.AI.Indexing.Interfaces.IIndexingPipelineStep next = null) : base(next) { }

        public override IndexingPipelineSteps StepType => IndexingPipelineSteps.Embed;

        protected override Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            // TODO:
            // - Generate embedding vector from EmbedSnippet
            // - Set WorkItem.Vector
            // - Set RagPayload.Meta.EmbeddingModel
            return Task.FromResult(InvokeResult.Success);
        }
    }
}
