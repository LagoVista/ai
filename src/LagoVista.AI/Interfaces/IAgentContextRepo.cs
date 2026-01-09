// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 865e3ca22ec622f6b896e30b7c25418418f9708f103dde24375b7333e2b1b96b
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI
{

    public interface IAgentContextLoaderRepo
    {
        Task<AgentContext> GetAgentContextAsync(string id);

    }

    public interface IAgentContextRepo
    {
        Task AddAgentContextAsync(AgentContext agentContext);
        Task UpdateAgentContextAsync(AgentContext agentContext);
        Task DeleteAgentContextAsync(string id);
        Task<AgentContext> GetAgentContextAsync(string id);
        Task<ListResponse<AgentContextSummary>> GetAgentContextSummariesForOrgAsync(string orgId, ListRequest listRequest);
        Task<bool> QueryKeyInUseAsync(string key, string org);

    }
}
