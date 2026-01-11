using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Indexing.Interfaces
{
    public interface IGitRepoInspector
    {
        InvokeResult<GitRepoInfo> GetRepoInfo(string workingDirectory);
    }
}
