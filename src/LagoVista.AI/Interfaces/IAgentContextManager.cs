using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IAgentContextManager
    {
        Task<InvokeResult> AddAgentContextAsync(LagoVista.AI.Models.AgentContext agentContext, EntityHeader org, EntityHeader user);
        Task<InvokeResult> UpdateAgentContextAsync(Models.AgentContext agentContext, EntityHeader org, EntityHeader user);
        Task<Models.AgentContext> GetAgentContextAsync(string id, EntityHeader org, EntityHeader user);
        Task<Models.AgentContext> GetAgentContextWithSecretsAsync(string id, EntityHeader org, EntityHeader user);
        Task<InvokeResult> DeleteAgentContextAsync(string id, EntityHeader org, EntityHeader user);
        Task<ListResponse<AgentContextSummary>> GetAgentContextsForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest);
    }
}
