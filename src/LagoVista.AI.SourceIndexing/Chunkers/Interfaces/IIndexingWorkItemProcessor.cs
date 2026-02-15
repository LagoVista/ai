using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.Interfaces
{
    /// <summary>
    /// Processes exactly one work item for a specific pipeline step.
    /// Implementations are typically selected by SubKind.
    /// </summary>
    public interface IIndexingWorkItemProcessor
    {
        Task<InvokeResult> ProcessAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem);
    }
}
