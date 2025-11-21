using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces
{
    public interface INormalizedChunkBuilder
    {
        Task<IReadOnlyList<NormalizedChunk>> BuildChunksAsync(
             IndexFileContext fileContext,
             DomainModelCatalog catalog,
             CancellationToken token = default);
    }
}
