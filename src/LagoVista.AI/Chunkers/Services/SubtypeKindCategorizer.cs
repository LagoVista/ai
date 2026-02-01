using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Indexing.Models;
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
            return Task.FromResult(InvokeResult.Success);
        }
    }
}
