using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.Models;
using System.Collections.Generic;
using System.Threading;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces
{
    public interface INormalizedChunkBuilderService
    {
        InvokeResult<List<NormalizedChunk>> BuildChunks(string filePath, DomainModelCatalog catalog, IReadOnlyDictionary<string, string> resources);
    }
}
