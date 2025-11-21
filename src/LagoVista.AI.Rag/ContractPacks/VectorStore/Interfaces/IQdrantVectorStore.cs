using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.ContractPacks.VectorStore.Interfaces
{
    public interface IQdrantVectorStore
    {
        Task IndexChunksAsync(DocumentIdentity document, IReadOnlyList<float[]> vectors, IReadOnlyList<string> normalizedTexts, IReadOnlyList<FacetValue> facets, CancellationToken cancellationToken = default);
        Task DeleteDocumentAsync(DocumentIdentity document, CancellationToken cancellationToken = default);
    }
}
