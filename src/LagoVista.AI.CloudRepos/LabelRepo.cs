using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.CloudRepos
{
    public class LabelRepo : DocumentDBRepoBase<Label>, ILabelRepo
    {
        private readonly bool _shouldConsolidateCollections;

        public LabelRepo(ITrainingDataSettings settings, IAdminLogger logger) :
            base(settings.LabelsConnectionSettings.Uri, settings.LabelsConnectionSettings.AccessKey, settings.LabelsConnectionSettings.ResourceName, logger)
        {
            _shouldConsolidateCollections = settings.ShouldConsolidateCollections;
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;

        public Task AddLabelAsync(Label label)
        {
            return this.CreateDocumentAsync(label);
        }

        public Task<Label> GetLabelAsync(string id)
        {
            return this.GetDocumentAsync(id);
        }

        public async Task<ListResponse<LabelSummary>> GetLabelsForOrgAsync(string orgId, ListRequest request)
        {
            var items = await base.QueryAsync(qry => qry.OwnerOrganization.Id == orgId);

            var summaries = from item in items
                            select item.CreateSummary();

            return ListResponse<LabelSummary>.Create(request, summaries);
        }

        public async Task<bool> QueryKeyInUseAsync(string key, string orgId)
        {
            var items = await base.QueryAsync(attr => (attr.OwnerOrganization.Id == orgId || attr.IsPublic == true) && attr.Key == key);
            return items.Any();
        }

        public async Task<ListResponse<LabelSummary>> SearchLabelsForOrgAsync(string orgId, string searchString, ListRequest request)
        {
            var items = await base.QueryAsync(qry => qry.OwnerOrganization.Id == orgId && qry.Name.Contains(searchString));

            var summaries = from item in items
                            select item.CreateSummary();

            return ListResponse<LabelSummary>.Create(request, summaries);
        }

        public Task UpdateLabelAsync(Label label)
        {
            return this.UpsertDocumentAsync(label);
        }
    }
}
