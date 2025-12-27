
using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IAgentToolBoxManager
    {
        Task<InvokeResult> AddAgentToolBoxAsync(LagoVista.AI.Models.AgentToolBox agentToolBox, EntityHeader org, EntityHeader user);
        Task<InvokeResult> UpdateAgentToolBoxAsync(Models.AgentToolBox agentToolBox, EntityHeader org, EntityHeader user);
        Task<Models.AgentToolBox> GetAgentToolBoxAsync(string id, EntityHeader org, EntityHeader user);
        Task<InvokeResult> DeleteAgentToolBoxAsync(string id, EntityHeader org, EntityHeader user);
        Task<ListResponse<AgentToolBoxSummary>> GetAgentToolBoxesForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest);
    }
}
