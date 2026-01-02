using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IAgentPersonaDefinitionManager
    {
        Task<InvokeResult> AddAgentPersonaDefinitionAsync(AgentPersonaDefinition agentPersonaDefinition, EntityHeader org, EntityHeader user);
        Task<InvokeResult> UpdateAgentPersonaDefinitionAsync(AgentPersonaDefinition agentPersonaDefinition, EntityHeader org, EntityHeader user);
        Task<AgentPersonaDefinition> GetAgentPersonaDefinitionAsync(string id, EntityHeader org, EntityHeader user);
        Task<InvokeResult> DeleteAgentPersonaDefinitionAsync(string id, EntityHeader org, EntityHeader user);
        Task<ListResponse<AgentPersonaDefinitionSummary>> GetAgentPersonaDefinitionsForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest);
    }
}
