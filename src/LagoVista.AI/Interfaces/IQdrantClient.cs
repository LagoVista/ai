using LagoVista.AI.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IQdrantClient
    {
        Task EnsureCollectionAsync(QdrantCollectionConfig cfg);
        Task UpsertAsync(string collection, IEnumerable<QdrantPoint> points);
        Task<List<QdrantScoredPoint>> SearchAsync(string collection, QdrantSearchRequest req);
        Task DeleteByIdsAsync(string collection, IEnumerable<string> ids);
    }
}
