using LagoVista.AI.Models;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    public class ModelLabelSetRepo : DocumentDBRepoBase<ModelLabelSet>, ILabelSetRepo
    {
        private readonly bool _shouldConsolidateCollections;

        public ModelLabelSetRepo(ITrainingDataSettings settings, IAdminLogger logger) :
            base(settings.LabelsConnectionSettings.Uri, settings.LabelsConnectionSettings.AccessKey, settings.LabelsConnectionSettings.ResourceName, logger)
        {
            _shouldConsolidateCollections = settings.ShouldConsolidateCollections;
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;

        public Task AddLabelAsync(ModelLabelSet label)
        {
            return this.CreateDocumentAsync(label);
        }

        public Task DeleteLabelSetAsync(string id)
        {
            return this.DeleteDocumentAsync(id);
        }

        public Task<ModelLabelSet> GetLabelSetAsync(string id)
        {
            return this.GetDocumentAsync(id);
        }

        public async Task<ListResponse<ModelLabelSetSummary>> GetLabelSetsForOrgAsync(string orgId, ListRequest listRequest)
        {
            var items = await base.QueryAsync(qry => qry.OwnerOrganization.Id == orgId);

            var summaries = from item in items
                            select item.CreateSummary();

            return ListResponse<ModelLabelSetSummary>.Create(listRequest, summaries);
        }

        public async Task<bool> QueryKeyInUseAsync(string key, string orgId)
        {
            var items = await base.QueryAsync(attr => (attr.OwnerOrganization.Id == orgId || attr.IsPublic == true) && attr.Key == key);
            return items.Any();
        }

        public Task UpdateLabelAsync(ModelLabelSet label)
        {
            return this.UpsertDocumentAsync(label);
        }
    }
}
