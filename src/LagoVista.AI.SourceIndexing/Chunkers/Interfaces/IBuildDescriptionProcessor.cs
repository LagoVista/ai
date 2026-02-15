using LagoVista.AI.Chunkers.Providers;
using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.Interfaces
{
    public interface IBuildDescriptionProcessor
    {
        Task<InvokeResult<IDescriptionProvider>> ProcessAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem);

    }
}
