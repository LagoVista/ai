using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.PipeLine
{
    public sealed class PersistSourceFileStep : IndexingPipelineStepBase, IPersistSourceFileStep
    {
        public PersistSourceFileStep(IExtractSymbolsStep next = null) : base(next) { }

        public override IndexingPipelineSteps StepType => IndexingPipelineSteps.PersistSourceFile;

        protected override Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            // This step is file-level in concept, but we still run under the work-item iteration contract.
            // TODO: Persist ctx.FullSource and populate ctx.FullSourceUrl.
            return Task.FromResult(InvokeResult.Success);
        }
    }
}
