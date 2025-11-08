// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: a31fb1cf58c54a671be9dacc39e0abae88831430f7e7d6ebc4e48ad383688109
// IndexVersion: 2
// --- END CODE INDEX META ---
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.CloudRepos
{
    public class AiConversationRepo : DocumentDBRepoBase<AiConversation>, IAiConversationRepo
    {
        private readonly bool _shouldConsolidateCollections;
        public AiConversationRepo(IMLRepoSettings repoSettings, IAdminLogger logger) :
            base(repoSettings.MLDocDbStorage.Uri, repoSettings.MLDocDbStorage.AccessKey, repoSettings.MLDocDbStorage.ResourceName, logger)
        {
            _shouldConsolidateCollections = repoSettings.ShouldConsolidateCollections;
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;
        public Task AddAiConversationAsync(AiConversation aiConversation)
        {
            return this.CreateDocumentAsync(aiConversation);
        }

        public Task DeleteAiConversationAsync(string id)
        {
            return base.DeleteDocumentAsync(id);
        }

        public Task<AiConversation> GetAiConversationAsync(string modelId)
        {
            return base.GetDocumentAsync(modelId);
        }

        public  Task<ListResponse<AiConversationSummary>> GetAiConversationSummariesForOrgAsync(string orgId, ListRequest listRequest)
        {
            return QuerySummaryAsync<AiConversationSummary, AiConversation>(rec => rec.OwnerOrganization.Id == orgId, rec => rec.LastUpdatedDate, listRequest); 
        }

        public Task<ListResponse<AiConversationSummary>> GetAiConversationSummariesForUserAsync(string orgId, string userId, ListRequest listRequest)
        {
            return QuerySummaryAsync<AiConversationSummary, AiConversation>(rec => rec.OwnerOrganization.Id == orgId && rec.CreatedBy.Id == userId, rec => rec.LastUpdatedDate, listRequest);
        }

        public async Task<bool> QueryKeyInUseAsync(string key, string orgId)
        {
            var items = await base.QueryAsync(attr => (attr.OwnerOrganization.Id == orgId || attr.IsPublic == true) && attr.Key == key);
            return items.Any();
        }

        public Task UpdateAiConversationAsync(AiConversation aiConversation)
        {
            return base.UpsertDocumentAsync(aiConversation);
        }
    }
}
