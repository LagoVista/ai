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
using LagoVista.IoT.Web.Common.Attributes;

namespace LagoVista.AI.Rest
{
    [ConfirmedUser]
    [AppBuilder]
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
        [HttpPost("/api/ml/model")]
        public Task<InvokeResult> AddModelAsync([FromBody] Model model)
        {
            return _mgr.AddModelAsync(model, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Model - Update
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut("/api/ml/model")]
        public Task<InvokeResult> UpdateModelAsync([FromBody] Model model)
        {
            SetUpdatedProperties(model);
            return _mgr.UpdateModelAsync(model, OrgEntityHeader, UserEntityHeader);
        }

        [HttpPost("/api/mlmodel/{modelid}/{revision}")]
        public Task<InvokeResult> UploadModel(string modelid, int revision, [FromBody] byte[] model)
        {
            return _mgr.UploadModel(modelid, revision, model, OrgEntityHeader, UserEntityHeader);
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
        [HttpGet("/api/ml/models")]
        public Task<ListResponse<ModelSummary>> GetModelsForOrg()
        {
            return _mgr.GetModelsForOrgAsync(OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }

        /// <summary>
        /// Model - Get Model
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/ml/model/{id}")]
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
        [HttpGet("/api/ml/model/factory")]
        public DetailResponse<Model> CreateNewModel()
        {
            var model = DetailResponse<Model>.Create();
            model.Model.Id = Guid.NewGuid().ToId();
            SetAuditProperties(model.Model);
            SetOwnedProperties(model.Model);
            return model;
        }

        /// <summary>
        /// Model - Create new Revision
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/ml/model/revision/factory")]
        public DetailResponse<ModelRevision> CreateNewModelRevision()
        {
            var model = DetailResponse<ModelRevision>.Create();
            model.Model.Id = Guid.NewGuid().ToId();
            model.Model.Datestamp = DateTime.UtcNow.ToString();
            return model;
        }

        /// <summary>
        /// Model - Create new Notes
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/ml/model/note/factory")]
        public DetailResponse<ModelNotes> CreateNewModelNote()
        {
            var model = DetailResponse<ModelNotes>.Create();
            model.Model.Datestamp = DateTime.UtcNow.ToString();
            model.Model.Id = Guid.NewGuid().ToId();
            return model;
        }

        /// <summary>
        /// Model - Create new Experiment
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/ml/model/experiment/factory")]
        public DetailResponse<Experiment> CreateNewModelExperiment()
        {
            var model = DetailResponse<Experiment>.Create();
            model.Model.Id = Guid.NewGuid().ToId();
            return model;
        }


        /// <summary>
        /// Model - Create new Label
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/ml/model/label/factory")]
        public DetailResponse<Label> CreateNewLabel()
        {
            var model = DetailResponse<Label>.Create();
            model.Model.Id = Guid.NewGuid().ToId();
            return model;
        }

        /// <summary>
        /// Model - Key In Use
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/ml/model/{key}/keyinuse")]
        public Task<bool> ModelKeyInUseAsync(String key)
        {
            return _mgr.QueryKeyInUse(key, OrgEntityHeader);
        }
    }
}
