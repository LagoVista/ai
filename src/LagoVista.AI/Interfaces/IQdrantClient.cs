// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 5b2c080042bfe2e3fe4594aa4ef4e493b9fa19e2bbdb0220c86013f9a90049a5
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.AI.Services.Qdrant;
using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Utils.Types;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IQdrantClient
    {
        Task EnsureCollectionAsync(string collectionName);
        Task UpsertAsync(string collectionName, IEnumerable<IRagPoint> points, CancellationToken ct);
        Task UpsertInBatchesAsync(string collectionName, IReadOnlyList<IRagPoint> points, int vectorDims, int? maxPerBatch = null, CancellationToken ct = default);
        Task<List<QdrantScoredPoint>> SearchAsync(string collectionName, QdrantSearchRequest req);
        Task DeleteByIdsAsync(string collectionName, IEnumerable<string> ids);
        Task DeleteByDocIdAsync(string collection, string docId, CancellationToken ct = default);

        Task DeleteByDocIdsAsync(string collection, IEnumerable<string> docIds, CancellationToken ct = default);
        Task DeleteByFilterAsync(string collection, QdrantFilter filter, CancellationToken ct = default);
    }
}
