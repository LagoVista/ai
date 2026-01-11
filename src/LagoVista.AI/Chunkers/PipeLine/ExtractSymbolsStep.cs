using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.PipeLine
{
    public sealed class ExtractSymbolsStep : IndexingPipelineStepBase
    {
        public ExtractSymbolsStep(LagoVista.AI.Indexing.Interfaces.IIndexingPipelineStep next = null) : base(next) { }

        public override IndexingPipelineSteps StepType => IndexingPipelineSteps.ExtractSymbols;

        protected override Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            // TODO:
            // - Parse ctx.FullSource
            // - Expand ctx.WorkItems to one per symbol
            // - For each symbol item: PointId = Guid.NewGuid(); populate symbol text + optional metadata
            return Task.FromResult(InvokeResult.Success);
        }
    }
}
