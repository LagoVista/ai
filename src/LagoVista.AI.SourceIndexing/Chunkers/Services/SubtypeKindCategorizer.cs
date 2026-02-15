using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Chunkers.Services
{
    public class SubtypeKindCategorizer : ISubtypeKindCategorizer
    {
        public Task<InvokeResult> ProcessAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {

            var result =  SourceKindAnalyzer.AnalyzeFile(workItem.Lenses.SymbolText, ctx.Resources.FileContext.RelativePath) ;
            workItem.Kind = result.SubKind;

            return Task.FromResult(InvokeResult.Success);
        }
    }
}
