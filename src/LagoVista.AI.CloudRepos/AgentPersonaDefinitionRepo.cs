using LagoVista.AI.Models;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.CloudStorage.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    public class AgentPersonaDefinitionRepo : DocumentDBRepoBase<AgentPersonaDefinition>, IAgentPersonaDefinitionRepo
    {
        private readonly bool _shouldConsolidateCollections;

        public AgentPersonaDefinitionRepo(IMLRepoSettings settings, IDocumentCloudCachedServices services) :
            base(settings.MLDocDbStorage.Uri, settings.MLDocDbStorage.AccessKey, settings.MLDocDbStorage.ResourceName, services)
        {
            _shouldConsolidateCollections = settings.ShouldConsolidateCollections;
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;

        public Task AddAgentPersonaDefinitionAsync(AgentPersonaDefinition VectorDatabase)
        {
            return this.CreateDocumentAsync(VectorDatabase);
        }

        public Task DeleteAgentPersonaDefinitionAsync(string id)
        {
            return this.DeleteDocumentAsync(id);  
        }

        public Task<AgentPersonaDefinition> GetAgentPersonaDefinitionAsync(string id)
        {
            return this.GetDocumentAsync(id);
        }

        public  Task<ListResponse<AgentPersonaDefinitionSummary>> GetAgentPersonaDefinitionSummariesForOrgAsync(string orgId, ListRequest request)
        {
            return QuerySummaryAsync<AgentPersonaDefinitionSummary, AgentPersonaDefinition>(vc => vc.OwnerOrganization.Id == orgId, vdb => vdb.Name, request);
        }
        
        public Task UpdateAgentPersonaDefinitionAsync(AgentPersonaDefinition VectorDatabase)
        {
            return this.UpsertDocumentAsync(VectorDatabase);
        }
    }
}

