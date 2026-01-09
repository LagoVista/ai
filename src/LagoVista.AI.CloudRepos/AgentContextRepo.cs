// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 6632a11efc12f336561908fce65ee9751ec0b1131b8205f4e91dc7e92733e887
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.CloudStorage.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.Azure.Cosmos.Linq;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
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

