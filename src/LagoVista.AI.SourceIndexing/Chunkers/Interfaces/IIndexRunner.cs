using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Rag.Chunkers.Models;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Chunkers.Interfaces
{
    public interface IIndexRunner
    {
        Task RunAsync(IngestionConfig config, string processRepo = null, SubtypeKind? subKindFilter = null, CancellationToken cancellationToken = default);
    }
}
