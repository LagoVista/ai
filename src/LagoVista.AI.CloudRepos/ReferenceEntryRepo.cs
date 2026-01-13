using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models.AuthoritativeAnswers;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.CloudStorage.Interfaces;
using LagoVista.Core.Models.UIMetaData;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    public class ReferenceEntryRepo : DocumentDBRepoBase<ReferenceEntry>, IReferenceEntryRepo
    {
        private readonly bool _shouldConsolidateCollections;

        public ReferenceEntryRepo(IMLRepoSettings settings, IDocumentCloudCachedServices services) :
            base(settings.MLDocDbStorage.Uri, settings.MLDocDbStorage.AccessKey, settings.MLDocDbStorage.ResourceName, services)
        {
            _shouldConsolidateCollections = settings.ShouldConsolidateCollections;
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;

        public Task AddReferenceEntryAsync(ReferenceEntry referenceEntry)
        {
            return CreateDocumentAsync(referenceEntry);
        }

        public Task UpdateReferenceEntryAsync(ReferenceEntry referenceEntry)
        {
            return UpsertDocumentAsync(referenceEntry);
        }

        public Task DeleteReferenceEntryAsync(string id)
        {
            return DeleteDocumentAsync(id);
        }

        public Task<ReferenceEntry> GetReferenceEntryAsync(string id)
        {
            return GetDocumentAsync(id);
        }

        public Task<ListResponse<ReferenceEntrySummary>> GetReferenceEntrySummariesForOrgAsync(string orgId, ListRequest listRequest)
        {
            // Org scoping is via OwnerOrganization on EntityBase.
            return QuerySummaryAsync<ReferenceEntrySummary, ReferenceEntry>(re => re.OwnerOrganization.Id == orgId, re => re.Name, listRequest);
        }

        public async Task<List<ReferenceEntry>> FindByNormalizedModelQuestionHashAsync(string orgId, string normalizedModelQuestionHash)
        {
            var matches = await QueryAsync(re => re.OwnerOrganization.Id == orgId &&
                                                re.IsActive == true &&
                                                re.NormalizedModelQuestionHash == normalizedModelQuestionHash);

            return matches.ToList();
        }
    }
}
