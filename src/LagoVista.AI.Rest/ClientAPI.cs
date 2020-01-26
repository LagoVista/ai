using LagoVista.AI.Models;
using LagoVista.AI.Models.TrainingData;
using LagoVista.Core.Models.UIMetaData;
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
        ILabelManager _lblManager;
        ISampleManager _sampleManager;
        IModelCategoryManager _modelCategoryManager;
        
        public ClientAPI(IExperimentResultManager experimentResultManager, IModelManager modelManager, IModelCategoryManager modelCategoryManager,
            ILabelManager lblManager, ISampleManager sampleMgr,
            UserManager<AppUser> userManager, IAdminLogger logger) : base(userManager, logger)
        {
            this._experimentResultManager = experimentResultManager ?? throw new ArgumentNullException(nameof(experimentResultManager));
            this._modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
            this._modelCategoryManager = modelCategoryManager ?? throw new ArgumentNullException(nameof(modelCategoryManager));
            this._lblManager = lblManager ?? throw new ArgumentNullException(nameof(lblManager));
            this._sampleManager = sampleMgr ?? throw new ArgumentNullException(nameof(sampleMgr));
        }

        [HttpPost("/clientapi/ml/sample")]
        public Task<InvokeResult<Sample>> UploadSampleAsync(IFormFile file, string tags)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            if (String.IsNullOrEmpty(tags))
            {
                throw new ArgumentNullException("Must pass in ?tags as a comma delimted set of non empty tags.");
            }

            var tagIds = new List<string>(tags.Split(','));
            using (var stream = file.OpenReadStream())
            {
                var sample = new byte[stream.Length];
                stream.Position = 0;
                stream.Read(sample, 0, (int)stream.Length);

                return _sampleManager.AddSampleAsync(sample, file.FileName, file.ContentType, tagIds, OrgEntityHeader, UserEntityHeader);
            }
        }


        [HttpGet("/clientapi/ml/samples/label/{labelid}")]
        public Task<ListResponse<SampleSummary>> GetSamplesForLabel(string labelid)
        {
            if (!Request.Headers.ContainsKey("Accept"))
            {
                throw new ArgumentNullException("must provide content type in accept header.");
            }

            var contentType = Request.Headers["Accept"];

            if(String.IsNullOrEmpty(contentType))
            {
                throw new ArgumentNullException("must provide content type in Accept header.");
            }

            return _sampleManager.GetSamplesForLabelAsync(labelid, contentType, OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }

        /// <summary>
        /// Sample - Get sample by id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/clientapi/ml/sample/{id}")]
        public async Task<IActionResult> GetSampleAsync(string id)
        {
            var sampleDetail = await _sampleManager.GetSampleDetailAsync(id, OrgEntityHeader, UserEntityHeader);
            var result = await _sampleManager.GetSampleAsync(id, OrgEntityHeader, UserEntityHeader);

            var ms = new MemoryStream(result.Result);
            return new FileStreamResult(ms, sampleDetail.ContentType);
        }

        /// <summary>
        /// Labels - Get for org
        /// </summary>
        /// <returns></returns>
        [HttpGet("/clientapi/ml/labels")]
        public Task<ListResponse<LabelSummary>> GetLabelsForOrgAsync()
        {
            return _lblManager.GetLabelsForOrgAsync(OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
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
