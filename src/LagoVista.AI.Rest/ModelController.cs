using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Identity;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.AspNetCore.Mvc;
using LagoVista.Core.Validation;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System;
using LagoVista.Core;

namespace LagoVista.AI.Rest
{
    public class ModelController : LagoVistaBaseController
    {
        readonly IModelManager _mgr;

        public ModelController(IModelManager modelManager, UserManager<AppUser> userManager, IAdminLogger logger) 
            : base(userManager, logger)
        {
            _mgr = modelManager;
        }

        /// <summary>
        /// Model - Add
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost("/api/model")]
        public Task<InvokeResult> AddInstanceAsync([FromBody] Model model)
        {
            return _mgr.AddModelAsync(model, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Model - Update
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut("/api/model")]
        public Task<InvokeResult> UpdateInstanceAsync([FromBody] Model model)
        {
            SetUpdatedProperties(model);
            return _mgr.UpdateModelAsync(model, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Model - Delete
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("/api/model/{id}")]
        public Task<InvokeResult> DeleteModelAsync(string id)
        {
            return _mgr.DeleteModelsync(id, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Model - Get all for org
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/models")]
        public Task<ListResponse<ModelSummary>> GetModelsForOrg()
        {
            return _mgr.GetModelsForOrgAsync(OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }

        /// <summary>
        /// Model - Get Model
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/model/{id}")]
        public async Task<DetailResponse<Model>> GetModelAsync(string id)
        {
            var model = await _mgr.GetModelAsync(id, OrgEntityHeader, UserEntityHeader);
            return DetailResponse<Model>.Create(model);
        }

        /// <summary>
        /// Model - Create new
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/model/factory")]
        public DetailResponse<Model> CreateNewModel()
        {
            var model = DetailResponse<Model>.Create();
            model.Model.Id = Guid.NewGuid().ToId();
            SetAuditProperties(model.Model);
            SetOwnedProperties(model.Model);
            return model;
        }

        /// <summary>
        /// Model - Create new
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/model/{id}/revision/factory")]
        public DetailResponse<ModelRevision> CreateNewModel(string id)
        {
            var model = DetailResponse<ModelRevision>.Create();
            model.Model.Id = Guid.NewGuid().ToId();
            return model;
        }

        /// <summary>
        /// Model - Key In Use
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/model/{key}/keyinuse")]
        public Task<bool> ModelKeyInUseAsync(String key)
        {
            return _mgr.QueryKeyInUse(key, OrgEntityHeader);
        }
    }
}
