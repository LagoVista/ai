using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.PipeLine
{
    public sealed class ExtractSymbolsStep : IndexingPipelineStepBase, IExtractSymbolsStep
    {
        IExtractSymbolsProcessorRegistry _extractSymbolRegistry;

        public ExtractSymbolsStep(ICategorizeContentStep next, IExtractSymbolsProcessorRegistry extrctSymbolService) : base(next)
        {
            _extractSymbolRegistry = extrctSymbolService ?? throw new ArgumentNullException(nameof(extrctSymbolService));
        }

        public override IndexingPipelineSteps StepType => IndexingPipelineSteps.ExtractSymbols;

        protected override Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            if (_extractSymbolRegistry.TryGet(ctx.Resources.FileContext.DocumentIdentity.Type, out IExtractSymbolsProcessor splitter))
            {
                return splitter.ProcessAsync(ctx, workItem);
            }
            else
            {
                return Task.FromResult(InvokeResult.Success);
            }
        }
    }
}
