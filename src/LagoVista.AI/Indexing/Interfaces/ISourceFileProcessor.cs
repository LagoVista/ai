using LagoVista.AI.Rag.Chunkers.Models;
using System.Collections.Generic;
using LagoVista.Core.Validation;
using LagoVista.AI.Models;
using System.Threading.Tasks;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Chunkers.Providers.DomainDescription;

namespace LagoVista.AI.Indexing.Interfaces
{
    public interface ISourceFileProcessor
    {
        Task<InvokeResult<ProcessedFileResults>> BuildChunks(IngestionConfig config, IndexFileContext indexFileContext, DomainModelCatalog catalog, SubtypeKind? subKindFilter, IReadOnlyDictionary<string, string> resources);
    }
}
