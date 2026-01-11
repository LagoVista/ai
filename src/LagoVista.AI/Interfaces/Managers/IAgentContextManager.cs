// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 893c5a63106998c0e5c1c059745574d9541883ced45e9e4d7be9df107bfddc54
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Managers
{
    public interface IAgentContextManager
    {
        Task<InvokeResult> AddAgentContextAsync(AgentContext agentContext, EntityHeader org, EntityHeader user);
        Task<InvokeResult> UpdateAgentContextAsync(AgentContext agentContext, EntityHeader org, EntityHeader user);
        Task<AgentContext> GetAgentContextAsync(string id, EntityHeader org, EntityHeader user);
        Task<AgentContext> GetAgentContextWithSecretsAsync(string id, EntityHeader org, EntityHeader user);
        Task<InvokeResult> DeleteAgentContextAsync(string id, EntityHeader org, EntityHeader user);
        Task<ListResponse<AgentContextSummary>> GetAgentContextsForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest);

        Task<InvokeResult> AddAgentModeAsync(string agentContextId, AgentMode agentMode, EntityHeader org, EntityHeader user);
    }
}
