using LagoVista.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    public class AgentPersonaDefinitionManager : ManagerBase, IAgentPersonaDefinitionManager
    {
        private readonly IAgentPersonaDefinitionRepo _repo;

        public AgentPersonaDefinitionManager(IAgentPersonaDefinitionRepo repo, IAdminLogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security)
                  : base(logger, appConfig, dependencyManager, security)
        {
            _repo = repo ?? throw new NullReferenceException(nameof(repo));
        }

        public async Task<InvokeResult> AddAgentPersonaDefinitionAsync(Models.AgentPersonaDefinition agentPersonaDefinition, EntityHeader org, EntityHeader user)
        {          
            await _repo.AddAgentPersonaDefinitionAsync(agentPersonaDefinition);
            return InvokeResult.Success;
        }

        public async Task<InvokeResult> DeleteAgentPersonaDefinitionAsync(string id, EntityHeader org, EntityHeader user)
        {
            var host = await _repo.GetAgentPersonaDefinitionAsync(id);

            await AuthorizeAsync(host, AuthorizeResult.AuthorizeActions.Delete, user, org);
            await ConfirmNoDepenenciesAsync(host);
            await _repo.DeleteAgentPersonaDefinitionAsync(id);
            return InvokeResult.Success;
        }

        public async Task<Models.AgentPersonaDefinition> GetAgentPersonaDefinitionAsync(string id, EntityHeader org, EntityHeader user)
        {
            var agentPersonaDefinition = await _repo.GetAgentPersonaDefinitionAsync(id);
            await AuthorizeAsync(agentPersonaDefinition, AuthorizeResult.AuthorizeActions.Read, user, org);
            return agentPersonaDefinition;
        }

        public async Task<ListResponse<AgentPersonaDefinitionSummary>> GetAgentPersonaDefinitionsForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest)
        {
            await AuthorizeOrgAccessAsync(user, org.Id, typeof(Models.AiModelLabel));
            return await _repo.GetAgentPersonaDefinitionSummariesForOrgAsync(org.Id, listRequest);
        }

        public async Task<InvokeResult> UpdateAgentPersonaDefinitionAsync(AgentPersonaDefinition db, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(db, Actions.Create);
            await AuthorizeAsync(db, AuthorizeResult.AuthorizeActions.Update, user, org);
            await _repo.UpdateAgentPersonaDefinitionAsync(db);
            return InvokeResult.Success;
        }
    }
}