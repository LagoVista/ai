// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 5b2c080042bfe2e3fe4594aa4ef4e493b9fa19e2bbdb0220c86013f9a90049a5
// IndexVersion: 2
// --- END CODE INDEX META ---
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
