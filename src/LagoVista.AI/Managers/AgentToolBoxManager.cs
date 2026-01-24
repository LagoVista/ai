// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: c8a9b0de2a6bf14f063c55f46c52d46ee0035077eb8b79425e721a25b3db6fe3
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    public class AgentToolBoxManager : ManagerBase, IAgentToolBoxManager
    {
        private readonly IAgentToolBoxRepo _repo;
    
        public AgentToolBoxManager(IAgentToolBoxRepo repo, IAdminLogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security)
                  : base(logger, appConfig, dependencyManager, security)
        {
            _repo = repo ?? throw new NullReferenceException(nameof(repo));
        }

        public async Task<InvokeResult> AddAgentToolBoxAsync(Models.AgentToolBox agentToolBox, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(agentToolBox, Actions.Create);

            await AuthorizeAsync(agentToolBox, AuthorizeResult.AuthorizeActions.Create, user, org);
            await _repo.AddAgentToolBoxAsync(agentToolBox);

            return InvokeResult.Success;
        }

        public async Task<InvokeResult> DeleteAgentToolBoxAsync(string id, EntityHeader org, EntityHeader user)
        {
            var host = await _repo.GetAgentToolBoxAsync(id);

            await AuthorizeAsync(host, AuthorizeResult.AuthorizeActions.Delete, user, org);
            await ConfirmNoDepenenciesAsync(host);
            await _repo.DeleteAgentToolBoxAsync(id);
            return InvokeResult.Success;
        }

        public async Task<Models.AgentToolBox> GetAgentToolBoxAsync(string id, EntityHeader org, EntityHeader user)
        {
            var agentToolBox = await _repo.GetAgentToolBoxAsync(id);
            await AuthorizeAsync(agentToolBox, AuthorizeResult.AuthorizeActions.Read, user, org);
            return agentToolBox;
        }

        public async Task<ListResponse<AgentToolBoxSummary>> GetAgentToolBoxesForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest)
        {
            await AuthorizeOrgAccessAsync(user, org.Id, typeof(Models.AiModelLabel));
            return await _repo.GetAgentToolBoxSummariesForOrgAsync(org.Id, listRequest);
        }

        public async Task<InvokeResult> UpdateAgentToolBoxAsync(AgentToolBox toolBox, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(toolBox, Actions.Create);
            await AuthorizeAsync(toolBox, AuthorizeResult.AuthorizeActions.Update, user, org);

   
            await _repo.UpdateAgentToolBoxAsync(toolBox);
            return InvokeResult.Success;
        }
    }
}