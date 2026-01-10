using LagoVista.AI.Models;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.CloudStorage.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    /// <summary>
    /// necessary for circular dependency avoidance
    /// </summary>
    public class AgentContextLoaderRepo : DocumentDBRepoBase<AgentContext> , IAgentContextLoaderRepo
    {
        private readonly bool _shouldConsolidateCollections;

        public AgentContextLoaderRepo(IMLRepoSettings settings, IAdminLogger logger, ICacheProvider cacheProvider) : 
            base(settings.MLDocDbStorage.Uri, settings.MLDocDbStorage.AccessKey, settings.MLDocDbStorage.ResourceName, logger, cacheProvider)
        {
            _shouldConsolidateCollections = settings.ShouldConsolidateCollections;
        }
        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;

        public Task<AgentContext> GetAgentContextAsync(string id)
        {
            return this.GetDocumentAsync(id);
        }

    }

    public class AgentContextRepo : DocumentDBRepoBase<AgentContext>, IAgentContextRepo
    {
        private readonly bool _shouldConsolidateCollections;

        public AgentContextRepo(IMLRepoSettings settings, IDocumentCloudCachedServices services) :
            base(settings.MLDocDbStorage.Uri, settings.MLDocDbStorage.AccessKey, settings.MLDocDbStorage.ResourceName, services)
        {
            _shouldConsolidateCollections = settings.ShouldConsolidateCollections;
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;

        public Task AddAgentContextAsync(AgentContext VectorDatabase)
        {
            return this.CreateDocumentAsync(VectorDatabase);
        }

        public Task DeleteAgentContextAsync(string id)
        {
            return this.DeleteDocumentAsync(id);  
        }

        public Task<AgentContext> GetAgentContextAsync(string id)
        {
            return this.GetDocumentAsync(id);
        }

        public  Task<ListResponse<AgentContextSummary>> GetAgentContextSummariesForOrgAsync(string orgId, ListRequest request)
        {
            return QuerySummaryAsync<AgentContextSummary, AgentContext>(vc => vc.OwnerOrganization.Id == orgId, vdb => vdb.Name, request);
        }

        public async Task<bool> QueryKeyInUseAsync(string key, string orgId)
        {
            var items = await base.QueryAsync(attr => (attr.OwnerOrganization.Id == orgId || attr.IsPublic == true) && attr.Key == key);
            return items.Any();
        }

        
        public Task UpdateAgentContextAsync(AgentContext VectorDatabase)
        {
            return this.UpsertDocumentAsync(VectorDatabase);
        }
    }
}

