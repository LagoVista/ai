using LagoVista.AI.Models.AuthoritativeAnswers;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Managers
{
    public interface IReferenceEntryManager
    {
        Task<InvokeResult> AddReferenceEntryAsync(ReferenceEntry referenceEntry, EntityHeader org, EntityHeader user);
        Task<InvokeResult> UpdateReferenceEntryAsync(ReferenceEntry referenceEntry, EntityHeader org, EntityHeader user);
        Task<ReferenceEntry> GetReferenceEntryAsync(string id, EntityHeader org, EntityHeader user);
        Task<InvokeResult> DeleteReferenceEntryAsync(string id, EntityHeader org, EntityHeader user);
        Task<ListResponse<ReferenceEntrySummary>> GetReferenceEntriesForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest);

        /// <summary>
        /// Lookup used by ask_agent_first.
        /// </summary>
        Task<AuthoritativeAnswerLookupResult> LookupAsync(string orgId, string modelQuestion);
    }
}
