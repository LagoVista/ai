using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Attributes;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI.Rest
{

    /// <summary>
    /// REST Class for Labels
    /// </summary>
    [ConfirmedUser]
    [AppBuilder]
    public class LabelController : LagoVistaBaseController
    {
        ILabelManager _labelManager;

        public LabelController(ILabelManager labelManager, UserManager<AppUser> userManager, IAdminLogger logger)
          : base(userManager, logger)
        {
            _labelManager = labelManager;
        }

        /// <summary>
        /// Label - Add
        /// </summary>
        /// <param name="label"></param>
        /// <returns></returns>
        [HttpPost("/api/ml/label")]
        public Task<InvokeResult> AddLabelAsync([FromBody] Label label)
        {
            return _labelManager.AddLabelAsync(label, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Label - Update
        /// </summary>
        /// <param name="label"></param>
        /// <returns></returns>
        [HttpPut("/api/ml/label")]
        public Task<InvokeResult> UpdateLabelAsync([FromBody] Label label)
        {
            SetUpdatedProperties(label);
            return _labelManager.UpdateLabelAsync(label, OrgEntityHeader, UserEntityHeader);
        }
  
        /// <summary>
        /// Label - Get
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/ml/label/{id}")]
        public async Task<DetailResponse<Label>> GetLabelAsync(string id)
        {
            return DetailResponse<Label>.Create(await _labelManager.GetLabelAsync(id, OrgEntityHeader, UserEntityHeader));
        }

        /// <summary>
        /// Label - Get
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/ml/label/factory")]
        public DetailResponse<Label> CreateLabelAsync()
        {
            var result = DetailResponse<Label>.Create();
            result.Model.Id = Guid.NewGuid().ToId();
            SetOwnedProperties(result.Model);
            SetAuditProperties(result.Model);
            return result;
        }


        /// <summary>
        /// Labels - Get for org
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/ml/labels")]
        public Task<ListResponse<LabelSummary>> GetLabelsForOrgAsync()
        {
            return _labelManager.GetLabelsForOrgAsync(OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }

        /// <summary>
        /// Labels - Search
        /// </summary>
        /// <param name="search">?search= Search text for labels.</param>
        /// <returns></returns>
        [HttpGet("/api/ml/labels/search")]
        public Task<ListResponse<LabelSummary>> GetLabelsForOrgAsync(string search)
        {
            return _labelManager.SearchLabelsAsync(search, OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }
    }
}
