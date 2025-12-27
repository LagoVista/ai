using LagoVista.AI.Models;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    internal class AgentToolBoxRepo : DocumentDBRepoBase<AgentToolBox>, IAgentToolBoxRepo
    {
        private readonly bool _shouldConsolidateCollections;

        public AgentToolBoxRepo(IMLRepoSettings settings, IAdminLogger logger, ICacheProvider cacheProvider) :
            base(settings.MLDocDbStorage.Uri, settings.MLDocDbStorage.AccessKey, settings.MLDocDbStorage.ResourceName, logger, cacheProvider)
        {
            _shouldConsolidateCollections = settings.ShouldConsolidateCollections;
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;

        public Task AddAgentToolBoxAsync(AgentToolBox agentToolBox)
        {
            return this.CreateDocumentAsync(agentToolBox);
        }

        public Task DeleteAgentToolBoxAsync(string id)
        {
            return this.DeleteDocumentAsync(id);
        }

        public Task<AgentToolBox> GetAgentToolBoxAsync(string id)
        {
            return this.GetDocumentAsync(id);
        }

        public Task<ListResponse<AgentToolBoxSummary>> GetAgentToolBoxSummariesForOrgAsync(string orgId, ListRequest listRequest)
        {
            return QuerySummaryAsync<AgentToolBoxSummary, AgentToolBox>(vc => vc.OwnerOrganization.Id == orgId, vdb => vdb.Name, listRequest);
        }

        public Task UpdateAgentToolBoxAsync(AgentToolBox agentToolBox)
        {
            return this.UpsertDocumentAsync(agentToolBox);
        }
    }
}
