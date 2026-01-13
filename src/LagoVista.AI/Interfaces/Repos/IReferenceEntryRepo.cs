using LagoVista.AI.Models.AuthoritativeAnswers;
using LagoVista.Core.Models.UIMetaData;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Repos
{
    public interface IReferenceEntryRepo
    {
        Task AddReferenceEntryAsync(ReferenceEntry referenceEntry);
        Task UpdateReferenceEntryAsync(ReferenceEntry referenceEntry);
        Task DeleteReferenceEntryAsync(string id);
        Task<ReferenceEntry> GetReferenceEntryAsync(string id);
        Task<ListResponse<ReferenceEntrySummary>> GetReferenceEntrySummariesForOrgAsync(string orgId, ListRequest listRequest);

        /// <summary>
        /// Minimal lookup support for ask_agent_first.
        /// V1: exact match on normalized model question hash.
        /// </summary>
        Task<List<ReferenceEntry>> FindByNormalizedModelQuestionHashAsync(string orgId, string normalizedModelQuestionHash);
    }
}
