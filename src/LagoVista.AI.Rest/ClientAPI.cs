// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 4d9aab7e66a688f0d51478a0cf648e31427a063e916b33084bff5f5ef7d17620
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Interfaces.Managers;
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
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.Rest
{
    [Authorize(AuthenticationSchemes = "APIToken")]
    public class ClientAPIController  : LagoVistaBaseController
    {
        readonly IExperimentResultManager _experimentResultManager;
        readonly IModelManager _modelMgr;


        public ClientAPIController(IModelManager modelManager, IExperimentResultManager experimentResultManager, UserManager<AppUser> userManager, IAdminLogger logger) : base(userManager, logger)
        {
            this._experimentResultManager = experimentResultManager ?? throw new ArgumentNullException(nameof(experimentResultManager));
            this._modelMgr = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
        }


        private String GetClaimValue(String claimId)
        {
            var claim = User.Claims.Where(clm => clm.Type == claimId).FirstOrDefault();
            var value = claim == null ? String.Empty : claim.Value;
            return value;
        }


        /// <summary>
        /// NuvAI - Post Experiment Result
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        [HttpPost("/client/api/ml/model/experiment/result")]
        public Task AddResultAsync([FromBody] ExperimentResult result)
        {
            return _experimentResultManager.AddExperimentResultAsync(result, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// NuvAI - Get Experiment Results for Revision
        /// </summary>
        /// <param name="id"></param>
        /// <param name="revision"></param>
        /// <returns></returns>
        [HttpGet("/client/api/ml/model/{id}/{revision}/experiment/results")]
        public Task GetResultsAsync(string id, int revision)
        {
            return _experimentResultManager.GetExperimentResultsAsync(id, revision, OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }

        /// <summary>
        /// Upload a specific model revision
        /// </summary>
        /// <param name="modelid"></param>
        /// <param name="revision"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        [HttpPost("/client/api/ml/model/{modelid}/{revision}")]
        [DisableRequestSizeLimit]
        public Task<InvokeResult<ModelRevision>> UploadModel(string modelid, int revision, IFormFile file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            using (var stream = file.OpenReadStream())
            {
                var model = new byte[stream.Length];
                stream.Position = 0;
                stream.ReadExactly(model, 0, (int)stream.Length);

                return _modelMgr.UploadModelAsync(modelid, revision, model, OrgEntityHeader, UserEntityHeader);
            }
        }
    }
}
