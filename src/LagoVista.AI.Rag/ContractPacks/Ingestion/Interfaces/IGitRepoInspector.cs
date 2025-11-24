using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces
{
    public interface IGitRepoInspector
    {
        InvokeResult<GitRepoInfo> GetRepoInfo(string workingDirectory);
    }
}
