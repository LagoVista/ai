using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IAgentConextRepo
    {
        Task AddAgentContextAsync(AgentContext agentContext);
        Task UpdateAgentContextAsync(AgentContext agentContext);
        Task DeleteAgentContextAsync(string id);
        Task<AgentContext> GetAgentContextAsync(string id);
        Task<ListResponse<AgentContextSummary>> GetAgentContextSummariesForOrgAsync(string orgId, ListRequest listRequest);
        Task<bool> QueryKeyInUseAsync(string key, string org);

    }
}
