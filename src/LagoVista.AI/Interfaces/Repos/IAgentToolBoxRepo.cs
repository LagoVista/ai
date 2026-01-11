using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Repos
{
    public interface IAgentToolBoxRepo
    {
        Task AddAgentToolBoxAsync(AgentToolBox agentToolBox);
        Task UpdateAgentToolBoxAsync(AgentToolBox agentToolBox);
        Task DeleteAgentToolBoxAsync(string id);
        Task<AgentToolBox> GetAgentToolBoxAsync(string id);
        Task<AgentToolBox> GetAgentToolBoxByKeyAsync(string orgId, string toolBoxKey);
        Task<ListResponse<AgentToolBoxSummary>> GetAgentToolBoxSummariesForOrgAsync(string orgId, ListRequest listRequest);

    }
}
