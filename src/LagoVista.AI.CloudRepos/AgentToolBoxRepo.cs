using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.CloudStorage.Interfaces;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    internal class AgentToolBoxRepo : DocumentDBRepoBase<AgentToolBox>, IAgentToolBoxRepo
    {
        private readonly bool _shouldConsolidateCollections;

        private readonly ICacheProvider _cacheProvider;

        public AgentToolBoxRepo(IMLRepoSettings settings, IDocumentCloudCachedServices services) :
            base(settings.MLDocDbStorage.Uri, settings.MLDocDbStorage.AccessKey, settings.MLDocDbStorage.ResourceName, services)
        {
            _cacheProvider = services.CacheProvider;
            _shouldConsolidateCollections = settings.ShouldConsolidateCollections;
        }

        private string ToolBoxCacheKey(string orgId, string key)
        {
            return $"{key}-{orgId}".ToLowerInvariant();
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;

        public async Task AddAgentToolBoxAsync(AgentToolBox agentToolBox)
        {
            var cacheKey = ToolBoxCacheKey(agentToolBox.OwnerOrganization.Id, agentToolBox.Key);
            await _cacheProvider.AddAsync(cacheKey, JsonConvert.SerializeObject(agentToolBox));
            await this.CreateDocumentAsync(agentToolBox);
        }

        public Task DeleteAgentToolBoxAsync(string id)
        {
            return this.DeleteDocumentAsync(id);
        }

        public Task<AgentToolBox> GetAgentToolBoxAsync(string id)
        {
            return this.GetDocumentAsync(id);
        }

        public async Task<AgentToolBox> GetAgentToolBoxByKeyAsync(string orgId, string toolBoxKey)
        {
            var cacheKey = ToolBoxCacheKey(orgId, toolBoxKey);
            var json = await _cacheProvider.GetAsync(cacheKey);
            if(!string.IsNullOrEmpty(json))
            {
                return JsonConvert.DeserializeObject<AgentToolBox>(json);
            }

            var results =  await QueryAsync(toolBox => toolBox.OwnerOrganization.Id == orgId && toolBox.Key == toolBoxKey);  
            if(results.Any())
            {
                var toolBox = results.First();
                await _cacheProvider.AddAsync(cacheKey, JsonConvert.SerializeObject(toolBox));
                return toolBox;
            }

            throw new RecordNotFoundException(nameof(AgentToolBox), cacheKey);
        }

        public Task<ListResponse<AgentToolBoxSummary>> GetAgentToolBoxSummariesForOrgAsync(string orgId, ListRequest listRequest)
        {
            return QuerySummaryAsync<AgentToolBoxSummary, AgentToolBox>(vc => vc.OwnerOrganization.Id == orgId, vdb => vdb.Name, listRequest);
        }

        public async Task UpdateAgentToolBoxAsync(AgentToolBox agentToolBox)
        {
            var cacheKey =ToolBoxCacheKey(agentToolBox.OwnerOrganization.Id, agentToolBox.Key);
            await _cacheProvider.AddAsync(cacheKey, JsonConvert.SerializeObject(agentToolBox));  
            await this.UpsertDocumentAsync(agentToolBox);
        }
    }
}
