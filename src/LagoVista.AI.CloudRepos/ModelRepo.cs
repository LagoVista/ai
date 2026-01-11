// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 9d8b5fe7bd4ce32acbd2140f7d2692b9e8c73d8ab4c77ed8b9d1a72eda42c46b
// IndexVersion: 2
// --- END CODE INDEX META ---
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.CloudRepos
{
    public class ModelRepo : DocumentDBRepoBase<Model>, IModelRepo
    {
        private readonly bool _shouldConsolidateCollections;
        public ModelRepo(IMLRepoSettings repoSettings, IAdminLogger logger) :
            base(repoSettings.MLDocDbStorage.Uri, repoSettings.MLDocDbStorage.AccessKey, repoSettings.MLDocDbStorage.ResourceName, logger)
        {
            _shouldConsolidateCollections = repoSettings.ShouldConsolidateCollections;
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;

        public Task AddModelAsync(Model model)
        {
            return base.CreateDocumentAsync(model);
        }

        public Task DeleteModelAsync(string id)
        {
            return base.DeleteDocumentAsync(id);
        }

        public Task<Model> GetModelAsync(string modelId)
        {
            return base.GetDocumentAsync(modelId);
        }

        public async Task<ListResponse<ModelSummary>> GetModelSummariesForCategoryAsync(string orgId, string categoryId, ListRequest listRequest)
        {
            var items = await base.QueryAsync(qry => qry.OwnerOrganization.Id == orgId && qry.ModelCategory.Id == categoryId);

            var summaries = from item in items
                            select item.CreateSummary();

            return ListResponse<ModelSummary>.Create(listRequest, summaries);
        }

        public async Task<ListResponse<ModelSummary>> GetModelSummariesForOrgAsync(string orgId, ListRequest listRequest)
        {
            var items = await base.QueryAsync(qry => qry.OwnerOrganization.Id == orgId);

            var summaries = from item in items
                            select item.CreateSummary();

            return ListResponse<ModelSummary>.Create(listRequest, summaries);
        }

        public async Task<bool> QueryKeyInUseAsync(string key, string orgId)
        {
            var items = await base.QueryAsync(attr => (attr.OwnerOrganization.Id == orgId || attr.IsPublic == true) && attr.Key == key);
            return items.Any();
        }

        public Task UpdateModelAsync(Model model)
        {
            return base.UpsertDocumentAsync(model);
        }
    }
}
