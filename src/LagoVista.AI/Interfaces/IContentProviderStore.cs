using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models.Context;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// AGN-030: Persistence boundary for the PromptKnowledgeProvider.
    ///
    /// The AgentRequestHandler owns when this interface is invoked.
    /// Implementations define where/how persistence occurs (out of scope for AGN-030).
    /// </summary>
    public interface IContentProviderStore
    {
        Task SaveAsync(string sessionId, PromptKnowledgeProvider provider, CancellationToken ct = default);
        Task<PromptKnowledgeProvider?> LoadAsync(string sessionId, CancellationToken ct = default);
    }
}
