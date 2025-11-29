using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using System.Collections.Generic;
using LagoVista.Core.Validation;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces
{
    public interface ISourceFileProcessor
    {
        InvokeResult<ProcessedFileResults> BuildChunks(IndexFileContext indexFileContext, DomainModelCatalog catalog, CodeSubKind? subKindFilter, IReadOnlyDictionary<string, string> resources);
    }
}
