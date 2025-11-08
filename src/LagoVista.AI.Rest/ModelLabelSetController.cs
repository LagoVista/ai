// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: f0b9634224fae4f5f5ba946a76150623a85ea75436495520dc330412d18beb6c
// IndexVersion: 2
// --- END CODE INDEX META ---
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
    public class ModelLabelSetController : LagoVistaBaseController
    {
        ILabelManager _labelManager;

        public ModelLabelSetController(ILabelManager labelManager, UserManager<AppUser> userManager, IAdminLogger logger)
          : base(userManager, logger)
        {
            _labelManager = labelManager;
        }

        /// <summary>
        /// Label - Add
        /// </summary>
        /// <param name="labelSet"></param>
        /// <returns></returns>
        [HttpPost("/api/ml/labelset")]
        public Task<InvokeResult> AddLabelSetAsync([FromBody] ModelLabelSet labelSet)
        {
            return _labelManager.AddLabelSetAsync(labelSet, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Label Set - Update
        /// </summary>
        /// <param name="labelSet"></param>
        /// <returns></returns>
        [HttpPut("/api/ml/labelset")]
        public Task<InvokeResult> UpdateLabelSetAsync([FromBody] ModelLabelSet labelSet)
        {
            SetUpdatedProperties(labelSet);
            return _labelManager.UpdateLabelSetAsync(labelSet, OrgEntityHeader, UserEntityHeader);
        }
  
        /// <summary>
        /// Label Set - Get
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/ml/labelset/{id}")]
        public async Task<DetailResponse<ModelLabelSet>> GetLabelSetAsync(string id)
        {
            return DetailResponse<ModelLabelSet>.Create(await _labelManager.GetLabelSetAsync(id, OrgEntityHeader, UserEntityHeader));
        }

        /// <summary>
        /// Label Set - Delete
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("/api/ml/labelset/{id}")]
        public async Task<InvokeResult> DeleteLabelSetAsync(string id)
        {
            return await _labelManager.DeleteLabelSetAsync(id, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Label - Get
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/ml/labelset/factory")]
        public DetailResponse<ModelLabelSet> CreateLabelSetAsync()
        {
            var result = DetailResponse<ModelLabelSet>.Create();
            result.Model.Id = Guid.NewGuid().ToId();
            SetOwnedProperties(result.Model);
            SetAuditProperties(result.Model);
            return result;
        }

        /// <summary>
        /// Label Sets - Get for org
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/ml/labelsets")]
        public Task<ListResponse<ModelLabelSetSummary>> GetLabelSetsForOrgAsync()
        {
            return _labelManager.GetLabelSetsForOrgAsync(OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }
    }
}
