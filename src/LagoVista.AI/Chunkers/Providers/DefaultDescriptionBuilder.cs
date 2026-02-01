using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Chunkers.Providers
{
    internal class DefaultDescriptionBuilder : IBuildDescriptionProcessor
    {
        public Task<InvokeResult> ProcessAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            return Task.FromResult(InvokeResult.Success);
        }
    }
}
