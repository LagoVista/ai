using LagoVista.AI.Models;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
