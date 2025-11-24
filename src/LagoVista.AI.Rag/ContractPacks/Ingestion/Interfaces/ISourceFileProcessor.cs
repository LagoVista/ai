using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using System.Collections.Generic;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces
{
    public interface ISourceFileProcessor
    {
        InvokeResult<List<NormalizedChunk>> BuildChunks(string filePath, DomainModelCatalog catalog, IReadOnlyDictionary<string, string> resources);
    }
}
