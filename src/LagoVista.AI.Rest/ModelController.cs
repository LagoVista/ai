﻿using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Identity;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.AspNetCore.Mvc;
using LagoVista.Core.Validation;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System;
using System.Linq;
using LagoVista.Core;
using LagoVista.IoT.Web.Common.Attributes;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace LagoVista.AI.Rest
{
    /// <summary>
    /// REST Class for Uploading Models
    /// </summary>
    [ConfirmedUser]
    [AppBuilder]
    public class ModelController : LagoVistaBaseController
    {
        readonly IModelManager _mgr;

        /// <summary>
        /// Construct a REST class for models
        /// </summary>
        /// <param name="modelManager"></param>
        /// <param name="userManager"></param>
        /// <param name="logger"></param>
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

        /// <summary>
        /// Upload a specific model revision
        /// </summary>
        /// <param name="modelid"></param>
        /// <param name="revision"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        [HttpPost("/api/ml/model/{modelid}/{revision}")]
        [DisableRequestSizeLimit]
        public Task<InvokeResult<ModelRevision>> UploadModel(string modelid, int revision, IFormFile file)
        {
            if(file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            using (var stream = file.OpenReadStream())
            {
                var model = new byte[stream.Length];
                stream.Position = 0;
                stream.Read(model, 0, (int)stream.Length);

                return _mgr.UploadModelAsync(modelid, revision, model, OrgEntityHeader, UserEntityHeader);
            }
        }

        /// <summary>
        /// Download a specific model revision
        /// </summary>
        /// <param name="modelid"></param>
        /// <param name="revision"></param>
        /// <returns></returns>
        [HttpGet("/api/ml/model/{modelid}/{revision}")]
        public async Task<IActionResult> GetMLModelAsync(string modelid, int revision)
        {
            var model = await _mgr.GetModelAsync(modelid, OrgEntityHeader, UserEntityHeader);

           var modelRevision = model.Revisions.First(md => md.VersionNumber == revision);

            var result = await _mgr.GetMLModelAsync(modelid, revision, OrgEntityHeader, UserEntityHeader);

            if(!result.Successful)
            {
                throw new Exception(result.Errors.First().Message);
            }

            var ms = new MemoryStream(result.Result);
            return new FileStreamResult(ms, "application/octet-stream")
            {
                FileDownloadName = modelRevision.FileName 
            };
        }

        /// <summary>
        /// Model - Delete
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("/api/ml/model/{id}")]
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
        /// Model - Get Revisions for a Model.
        /// </summary>
        /// <param name="modelid"></param>
        /// <returns></returns>
        [HttpGet("/api/ml/model/{modelid}/revisions")]
        public async Task<ListResponse<ModelRevision>> GetModelRevisionsForModel(string modelid)
        {
            var model = await _mgr.GetModelAsync(modelid, OrgEntityHeader, UserEntityHeader);
            return ListResponse<ModelRevision>.Create(model.Revisions);
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
        /// <returns></returns>
        [HttpGet("/api/ml/model/experiment/factory")]
        public DetailResponse<Experiment> CreateNewModelExperiment()
        {
            var model = DetailResponse<Experiment>.Create();
            model.Model.Id = Guid.NewGuid().ToId();
            return model;
        }


        /// <summary>
        /// Model - Create new preprocessor
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/ml/model/preprocessor/factory")]
        public DetailResponse<Preprocessor> CreateNewPreprocessor()
        {
            var model = DetailResponse<Preprocessor>.Create();
            model.Model.Id = Guid.NewGuid().ToId();
            return model;
        }

        /// <summary>
        /// Model - Create new preprocessor setting
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/ml/model/preprocessor/setting/factory")]
        public DetailResponse<PreprocessorSetting> CreateNewPreprocessorSetting()
        {
            var model = DetailResponse<PreprocessorSetting>.Create();
            model.Model.Id = Guid.NewGuid().ToId();
            return model;
        }

        /// <summary>
        /// Model - Create new preprocessor setting
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/ml/model/setting/factory")]
        public DetailResponse<ModelSetting> CreateNewModelSetting()
        {
            var model = DetailResponse<ModelSetting>.Create();
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
