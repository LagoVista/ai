using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.Core.Utils.Types;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IQdrantClient
    {
        Task EnsureCollectionAsync(QdrantCollectionConfig cfg, string collectionName);
        Task UpsertAsync(string collectionName, IEnumerable<PayloadBuildResult> points, CancellationToken ct);
        Task UpsertInBatchesAsync(string collectionName, IReadOnlyList<PayloadBuildResult> points, int vectorDims, int? maxPerBatch = null, CancellationToken ct = default);
        Task<List<QdrantScoredPoint>> SearchAsync(string collectionName, QdrantSearchRequest req);
        Task DeleteByIdsAsync(string collectionName, IEnumerable<string> ids);
    }
}
