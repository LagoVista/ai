using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.PipeLine
{
    public sealed class UploadContentStep : IndexingPipelineStepBase, IUploadContentStep
    {
        public UploadContentStep(LagoVista.AI.Indexing.Interfaces.IIndexingPipelineStep next = null) : base(next) { }

        public override IndexingPipelineSteps StepType => IndexingPipelineSteps.UploadContent;

        protected override Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            // TODO:
            // - Persist per-work-item artifacts
            // - Populate RagPayload.Extra.ModelContentUrl, HumanContentUrl, SymbolContentUrl
            // - If issues exist, set IssuesContentUrl and HasIssues
            return Task.FromResult(InvokeResult.Success);
        }
    }
}
