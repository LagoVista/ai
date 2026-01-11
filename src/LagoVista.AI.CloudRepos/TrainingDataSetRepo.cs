// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 28b6f10874e9e1c8ec362e4eada95c3f57ee7eda5ef79ccf2bc21533bfcb63dc
// IndexVersion: 2
// --- END CODE INDEX META ---
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models.TrainingData;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.CloudRepos
{
    public class TrainingDataSetRepo : DocumentDBRepoBase<TrainingDataSet>, ITrainingDataSetRepo
    {
        private readonly bool _shouldConsolidateCollections;

        public TrainingDataSetRepo(ITrainingDataSettings settings, IAdminLogger logger) :
            base(settings.TrainingDataSetsConnectionSettings.Uri, settings.TrainingDataSetsConnectionSettings.AccessKey, settings.TrainingDataSetsConnectionSettings.ResourceName, logger)
        {
            _shouldConsolidateCollections = settings.ShouldConsolidateCollections;
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;

        public Task AddTrainingDataSetsAsync(TrainingDataSet dataSet)
        {
            return this.CreateDocumentAsync(dataSet);
        }

        public Task<TrainingDataSet> GetTrainingDataSetAsync(string id)
        {
            return base.GetDocumentAsync(id);
        }

        public async Task<ListResponse<TrainingDataSetSummary>> GetTrainingDataSetsForOrgAsync(string orgId, ListRequest request)
        {
            var items = await base.QueryAsync(qry => qry.OwnerOrganization.Id == orgId);

            var summaries = from item in items
                            select item.GetSummary();

            return ListResponse<TrainingDataSetSummary>.Create(request, summaries);
        }

        public async Task<bool> QueryKeyInUseAsync(string key, string orgId)
        {
            var items = await base.QueryAsync(attr => (attr.OwnerOrganization.Id == orgId || attr.IsPublic == true) && attr.Key == key);
            return items.Any();
        }

        public Task UpdateTrainingDataSetsAsync(TrainingDataSet dataSet)
        {
            return this.UpsertDocumentAsync(dataSet);
        }
    }
}
