using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IAgentPersonaDefinitionRepo
    {
        Task AddAgentPersonaDefinitionAsync(AgentPersonaDefinition agentPersonaDefinition);
        Task UpdateAgentPersonaDefinitionAsync(AgentPersonaDefinition agentPersonaDefinition);
        Task DeleteAgentPersonaDefinitionAsync(string id);
        Task<AgentPersonaDefinition> GetAgentPersonaDefinitionAsync(string id);
        Task<ListResponse<AgentPersonaDefinitionSummary>> GetAgentPersonaDefinitionSummariesForOrgAsync(string orgId, ListRequest listRequest);
    }
}
