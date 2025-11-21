using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.ContractPacks.Embedding.Interfaces
{
    public interface IEmbeddingService
    {
        Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    }
}
