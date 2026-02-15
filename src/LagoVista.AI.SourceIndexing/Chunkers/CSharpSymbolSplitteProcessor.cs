using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using PdfSharpCore.Pdf.IO;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.Chunkers
{
    internal class CSharpSymbolExtractorProcessor : IExtractSymbolsProcessor
    {
        ICSharpSymbolSplitterService _splitterService;

        public CSharpSymbolExtractorProcessor(ICSharpSymbolSplitterService splitterService)
        {
            _splitterService = splitterService ?? throw new ArgumentNullException(nameof(splitterService));  
        }

        public Task<InvokeResult> ProcessAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            var symbols = _splitterService.Split(ctx.Resources.FileContext.Contents);

            if (!symbols.Successful) return Task.FromResult(symbols.ToInvokeResult());

            if (symbols.Result.Count == 0) return Task.FromResult(InvokeResult.FromError("No symbols found"));

            // Only one primary symbol don't break it up.
            var firstResult = symbols.Result[0];
            workItem.RagPayload = firstResult.GetPayload(ctx.Resources.FileContext);
            workItem.Lenses = new Indexing.EntityIndexLenses()
            {
                SymbolText = symbols.Result[0].Text
            };
   
            for(var idx = 1; idx < symbols.Result.Count; ++idx)
            {
                var symbol = symbols.Result[idx];   
                var newWorkItem = ctx.CloneAndAddChild(workItem);
                newWorkItem.RagPayload.Extra.SymbolType = symbol.SymbolKind;
                newWorkItem.RagPayload.Extra.SymbolName = symbol.SymbolName;
                newWorkItem.RagPayload.Extra.SymbolFullName = symbol.SymbolName;
                newWorkItem.Lenses = new Indexing.EntityIndexLenses()
                {
                    SymbolText = symbol.Text
                };
            }
            
            return Task.FromResult(InvokeResult.Success);
        }
    }
}
