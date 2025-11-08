// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 8c18c8ab62f606ea233e79bb94f3ad502f4da3e01c91de0a2de9055bb66d2ca9
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
    public class ModelCategoryRepo : DocumentDBRepoBase<ModelCategory>, IModelCategoryRepo
    {
        private readonly bool _shouldConsolidateCollections;
        public ModelCategoryRepo(IMLRepoSettings repoSettings, IAdminLogger logger) :
            base(repoSettings.MLDocDbStorage.Uri, repoSettings.MLDocDbStorage.AccessKey, repoSettings.MLDocDbStorage.ResourceName, logger)
        {
            _shouldConsolidateCollections = repoSettings.ShouldConsolidateCollections;
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;
        public Task AddModelCategoryAsync(ModelCategory modelCategory)
        {
            return this.CreateDocumentAsync(modelCategory);
        }

        public Task DeleteModelCategoryAsync(string id)
        {
            return base.DeleteDocumentAsync(id);
        }

        public Task<ModelCategory> GetModelCategoryAsync(string modelId)
        {
            return base.GetDocumentAsync(modelId);
        }

        public async Task<ListResponse<ModelCategorySummary>> GetModelCategorySummariesForOrgAsync(string orgId, ListRequest listRequest)
        {
            var items = await base.QueryAsync(qry => qry.OwnerOrganization.Id == orgId);

            var summaries = from item in items
                            select item.CreateSummary();

            return ListResponse<ModelCategorySummary>.Create(listRequest, summaries);
        }

        public async Task<bool> QueryKeyInUseAsync(string key, string orgId)
        {
            var items = await base.QueryAsync(attr => (attr.OwnerOrganization.Id == orgId || attr.IsPublic == true) && attr.Key == key);
            return items.Any();
        }

        public Task UpdateModelCategoryAsync(ModelCategory modelCategory)
        {
            return base.UpsertDocumentAsync(modelCategory);
        }
    }
}
