using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces
{
    public interface IChunkBuilderForSymbol
    {
        InvokeResult<IReadOnlyList<NormalizedChunk>> CreateChunksForModel(string symbolText, string fileName, IChunkerServices chunkerService, DomainModelCatalog domainModelCatalog, IDictionary<string, string> resources);
        InvokeResult<IReadOnlyList<NormalizedChunk>> CreateChunksForManager(string symbolText, string fileName, DomainModelCatalog domainModelCatalog, IDictionary<string, string> resources);
        InvokeResult<IReadOnlyList<NormalizedChunk>> CreateChunksForController(string symbolText, string fileName, DomainModelCatalog domainModelCatalog, IDictionary<string, string> resources);
        InvokeResult<IReadOnlyList<NormalizedChunk>> CreateChunksForInterface(string symbolText, string fileName, DomainModelCatalog domainModelCatalog, IDictionary<string, string> resources);
    }
}
