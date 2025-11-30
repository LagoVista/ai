using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using System.Collections.Generic;
using LagoVista.Core.Validation;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Models;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces
{
    public interface ISourceFileProcessor
    {
        Task<InvokeResult<ProcessedFileResults>> BuildChunks(IngestionConfig config , IndexFileContext indexFileContext, DomainModelCatalog catalog, CodeSubKind? subKindFilter, IReadOnlyDictionary<string, string> resources);
    }
}
