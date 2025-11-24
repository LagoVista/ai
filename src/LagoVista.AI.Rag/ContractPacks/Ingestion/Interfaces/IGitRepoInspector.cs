using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces
{
    public interface IGitRepoInspector
    {
        InvokeResult<RepoInfo> GetRepoInfo(string workingDirectory);
    }
}
