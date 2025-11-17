using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentSessionRepo
    {
        Task AddSessionAsync(AgentSession session);
        Task UpdateSessionAsyunc(AgentSession session);
        Task<AgentSession> GetAgentSessionAsync(string agentSessionId);
        Task<ListResponse<AgentSessionSummary>> GetSessionSummariesForOrgAsync(string orgId, ListRequest listRequest);
        Task<ListResponse<AgentSessionSummary>> GetSessionSummariesForUserAsync(string orgId, string userId, ListRequest listRequest);
    }
}
