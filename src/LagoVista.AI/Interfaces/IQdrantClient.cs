using LagoVista.AI.Services;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IQdrantClient
    {
        Task EnsureCollectionAsync(QdrantCollectionConfig cfg);
        Task UpsertAsync(string collection, IEnumerable<QdrantPoint> points, CancellationToken ct);
        Task UpsertInBatchesAsync(string collection, IReadOnlyList<QdrantPoint> points, int vectorDims, int? maxPerBatch = null, CancellationToken ct = default);
        Task<List<QdrantScoredPoint>> SearchAsync(string collection, QdrantSearchRequest req);
        Task DeleteByIdsAsync(string collection, IEnumerable<string> ids);
    }
}
