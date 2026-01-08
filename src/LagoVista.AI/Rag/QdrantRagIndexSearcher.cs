using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag
{
    /// <summary>
    /// Adapter over an existing Qdrant client that exposes:
    ///   Task&lt;List&lt;QdrantScoredPoint&gt;&gt; SearchAsync(string collection, QdrantSearchRequest req)
    ///
    /// NOTE: Uses dynamic/object to minimize namespace/type coupling during initial generation.
    /// Replace with concrete Qdrant client + request/response types in your solution.
    /// </summary>
    public sealed class QdrantRagIndexSearcher : IRagIndexSearcher
    {
        private readonly object _qdrantClient;

        public QdrantRagIndexSearcher(object qdrantClient)
        {
            _qdrantClient = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
        }

        public async Task<IReadOnlyList<object>> SearchAsync(string collection, float[] vector, int limit, object ragScopeFilter)
        {
            if (String.IsNullOrWhiteSpace(collection)) throw new ArgumentNullException(nameof(collection));
            if (vector == null) throw new ArgumentNullException(nameof(vector));

            dynamic client = _qdrantClient;

            // Build a QdrantSearchRequest-like object dynamically.
            // Expected properties: Vector, Limit, WithPayload, Filter
            dynamic req = new System.Dynamic.ExpandoObject();
            req.Vector = vector;
            req.Limit = limit;
            req.WithPayload = true;
            req.Filter = ragScopeFilter;

            var results = await client.SearchAsync(collection, req);

            // Return as object list to keep this adapter decoupled.
            var list = new List<object>();
            foreach (var r in results)
                list.Add(r);

            return list;
        }
    }
}
