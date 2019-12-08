using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.Rest
{

    [Authorize(AuthenticationSchemes = "APIToken")]
    public class ClientAPI : LagoVistaBaseController
    {
        IExperimentResultManager _experimentResultManager;
        IModelManager _modelManager;
        IModelCategoryManager _modelCategoryManager;
        
        public ClientAPI(IExperimentResultManager experimentResultManager, IModelManager modelManager, IModelCategoryManager modelCategoryManager,
            UserManager<AppUser> userManager, IAdminLogger logger) : base(userManager, logger)
        {
            this._experimentResultManager = experimentResultManager;
            this._modelManager = modelManager;
            this._modelCategoryManager = modelCategoryManager;
        }

        [HttpPost("/clientapi/ml/model/{modelid}/{revision}")]
        public Task<InvokeResult> UploadModel(string modelid, int revision, IFormFile file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            using (var stream = file.OpenReadStream())
            {
                var model = new byte[stream.Length];
                stream.Position = 0;
                stream.Read(model, 0, (int)stream.Length);

                return _modelManager.UploadModel(modelid, revision, model, OrgEntityHeader, UserEntityHeader);
            }
        }

        [HttpPost("/clientapi/ml/model/{modelid}")]
        public Task<InvokeResult<ModelRevision>> UploadRevision(string modelId, [FromBody] ModelRevision revision)
        {
            return _modelManager.AddRevisionAsync(modelId, revision, OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/clientapi/ml/models/category/{categoryid}")]
        public async Task<IEnumerable<ModelSummary>> GetModelSummariesAsync(string categoryid)
        {
            var result = await this._modelManager.GetModelsForCategoryAsync(categoryid, OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
            return result.Model;
        }

        [HttpGet("/clientapi/ml/modelcategories")]
        public async Task<IEnumerable<ModelCategorySummary>> GetModelCategorySummariesAsync()
        {
            var result = await this._modelCategoryManager.GetModelCategoriesForOrgAsync(OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
            return result.Model;
        }

        [HttpGet("/clientapi/ml/model/{id}")]
        public Task<Model> GetModelAsync(string id)
        {
            return _modelManager.GetModelAsync(id, OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/clientapi/ml/mlmodel/{modelid}/{revisionid}")]
        public async Task<IActionResult> GetMLModelAsync(string modelid, int revisionid)
        {
            var result = await _modelManager.GetMLModelAsync(modelid, revisionid, OrgEntityHeader, UserEntityHeader);

            if (!result.Successful)
            {
                throw new Exception(result.Errors.First().Message);
            }

            var ms = new MemoryStream(result.Result);
            return new FileStreamResult(ms, "application/octet-stream");
        }

        [HttpPost("/clientapi/ml/model/experiment/result")]
        public Task AddResultAsync([FromBody] ExperimentResult result)
        {
            return _experimentResultManager.AddExperimentResultAsync(result, OrgEntityHeader, UserEntityHeader);
        }
    }
}
