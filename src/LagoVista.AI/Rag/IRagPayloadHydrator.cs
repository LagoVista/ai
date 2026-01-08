using System.Collections.Generic;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag
{
    /// <summary>
    /// Converts vector-search payload "index cards" into model-ready content.
    /// Input payload shape is QdrantScoredPoint.Payload (Dictionary&lt;string, object&gt;).
    /// </summary>
    public interface IRagPayloadHydrator
    {
        Task<IReadOnlyList<RagHydratedItem>> HydrateAsync(IReadOnlyList<Dictionary<string, object>> payloads);
    }
}
