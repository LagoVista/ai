
using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Managers
{
    public interface IAgentToolBoxManager
    {
        Task<InvokeResult> AddAgentToolBoxAsync(AgentToolBox agentToolBox, EntityHeader org, EntityHeader user);
        Task<InvokeResult> UpdateAgentToolBoxAsync(AgentToolBox agentToolBox, EntityHeader org, EntityHeader user);
        Task<AgentToolBox> GetAgentToolBoxAsync(string id, EntityHeader org, EntityHeader user);
        Task<InvokeResult> DeleteAgentToolBoxAsync(string id, EntityHeader org, EntityHeader user);
        Task<ListResponse<AgentToolBoxSummary>> GetAgentToolBoxesForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest);
    }
}
