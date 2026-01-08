using System.Collections.Generic;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag
{
    /// <summary>
    /// Performs vector search and returns scored points with payloads.
    /// This is a thin abstraction over the existing Qdrant search client.
    /// </summary>
    public interface IRagIndexSearcher
    {
        Task<IReadOnlyList<object>> SearchAsync(string collection, float[] vector, int limit, object ragScopeFilter);
    }
}
