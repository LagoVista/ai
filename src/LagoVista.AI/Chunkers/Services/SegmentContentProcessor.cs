using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Chunkers.Services
{
    public class SegmentContentProcessor : ISegmentContentProcessor
    {
        public Task<InvokeResult> ProcessAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            var chunks = RoslynCSharpChunker.Chunk(workItem.Lenses.SymbolText, ctx.Resources.FileContext.RelativePath);
            return Task.FromResult(InvokeResult.Success);
        
        }
    }
}
