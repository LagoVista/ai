// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 8467360d02a01a8d1545b33194ffc1f31d2e8bc7e33842d607cbeed26449229a
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
    public class LabelRepo : DocumentDBRepoBase<AiModelLabel>, ILabelRepo
    {
        private readonly bool _shouldConsolidateCollections;

        public LabelRepo(ITrainingDataSettings settings, IAdminLogger logger) :
            base(settings.LabelsConnectionSettings.Uri, settings.LabelsConnectionSettings.AccessKey, settings.LabelsConnectionSettings.ResourceName, logger)
        {
            _shouldConsolidateCollections = settings.ShouldConsolidateCollections;
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;

        public Task AddLabelAsync(AiModelLabel label)
        {
            return this.CreateDocumentAsync(label);
        }

        public Task<AiModelLabel> GetLabelAsync(string id)
        {
            return this.GetDocumentAsync(id);
        }

        public async Task<ListResponse<AiModelLabelSummary>> GetLabelsForOrgAsync(string orgId, ListRequest request)
        {
            var items = await base.QueryAsync(qry => qry.OwnerOrganization.Id == orgId);

            var summaries = from item in items
                            select item.CreateSummary();

            return ListResponse<AiModelLabelSummary>.Create(request, summaries);
        }

        public async Task<bool> QueryKeyInUseAsync(string key, string orgId)
        {
            var items = await base.QueryAsync(attr => (attr.OwnerOrganization.Id == orgId || attr.IsPublic == true) && attr.Key == key);
            return items.Any();
        }

        public async Task<ListResponse<AiModelLabelSummary>> SearchLabelsForOrgAsync(string orgId, string searchString, ListRequest request)
        {
            var items = await base.QueryAsync(qry => qry.OwnerOrganization.Id == orgId && qry.Name.Contains(searchString));

            var summaries = from item in items
                            select item.CreateSummary();

            return ListResponse<AiModelLabelSummary>.Create(request, summaries);
        }

        public Task UpdateLabelAsync(AiModelLabel label)
        {
            return this.UpsertDocumentAsync(label);
        }
    }
}
