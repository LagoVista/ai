using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Models.AuthoritativeAnswers;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Attributes;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LagoVista.AI.Rest
{
    [ConfirmedUser]
    [AppBuilder]
    public class ReferenceEntryController : LagoVistaBaseController
    {
        private readonly IReferenceEntryManager _referenceEntryManager;

        public ReferenceEntryController(IReferenceEntryManager referenceEntryManager, UserManager<AppUser> userManager, IAdminLogger logger) : base(userManager, logger)
        {
            _referenceEntryManager = referenceEntryManager;
        }

        [HttpGet("/api/referenceentry/{id}")]
        public async Task<DetailResponse<ReferenceEntry>> GetReferenceEntry(string id)
        {
            var entry = await _referenceEntryManager.GetReferenceEntryAsync(id, OrgEntityHeader, UserEntityHeader);
            return DetailResponse<ReferenceEntry>.Create(entry);
        }

        [HttpGet("/api/referenceentry/factory")]
        public DetailResponse<ReferenceEntry> CreateReferenceEntry()
        {
            var result = DetailResponse<ReferenceEntry>.Create();
            SetAuditProperties(result.Model);
            SetOwnedProperties(result.Model);
            return result;
        }

        [HttpGet("/api/referenceentries")]
        public Task<ListResponse<ReferenceEntrySummary>> GetReferenceEntries()
        {
            return _referenceEntryManager.GetReferenceEntriesForOrgAsync(OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }

        [HttpDelete("/api/referenceentry/{id}")]
        public Task<InvokeResult> DeleteReferenceEntryAsync(string id)
        {
            return _referenceEntryManager.DeleteReferenceEntryAsync(id, OrgEntityHeader, UserEntityHeader);
        }

        [HttpPost("/api/referenceentry")]
        public Task<InvokeResult> AddReferenceEntry([FromBody] ReferenceEntry entry)
        {
            return _referenceEntryManager.AddReferenceEntryAsync(entry, OrgEntityHeader, UserEntityHeader);
        }

        [HttpPut("/api/referenceentry")]
        public Task<InvokeResult> UpdateReferenceEntry([FromBody] ReferenceEntry entry)
        {
            SetUpdatedProperties(entry);
            return _referenceEntryManager.UpdateReferenceEntryAsync(entry, OrgEntityHeader, UserEntityHeader);
        }
    }
}
