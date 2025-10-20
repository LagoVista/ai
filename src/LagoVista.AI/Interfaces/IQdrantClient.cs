﻿using LagoVista.AI.Models;
using LagoVista.AI.Services;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IQdrantClient
    {
        void Init(VectorDatabase db);

        Task EnsureCollectionAsync(QdrantCollectionConfig cfg, string collectionName);
        Task UpsertAsync(string collectionName, IEnumerable<QdrantPoint> points, CancellationToken ct);
        Task UpsertInBatchesAsync(string collectionName, IReadOnlyList<QdrantPoint> points, int vectorDims, int? maxPerBatch = null, CancellationToken ct = default);
        Task<List<QdrantScoredPoint>> SearchAsync(string collectionName, QdrantSearchRequest req);
        Task DeleteByIdsAsync(string collectionName, IEnumerable<string> ids);
    }
}
