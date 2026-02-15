using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.Interfaces
{
    public interface IIndexingPipelineStep
    {
        IndexingPipelineSteps StepType { get; }
        Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx);
    }
}