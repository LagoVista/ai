// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: d12f8f4489001913592341043d480514e157a0c401cda7578a624c53c08d5c2c
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.TrainingData;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace LagoVista.AI.Rest
{

    public class SampleController : LagoVistaBaseController
    {
        private ISampleManager _sampleManager;

        public SampleController(ISampleManager sampleManager, UserManager<AppUser> userManager, IAdminLogger logger)
          : base(userManager, logger)
        {
            _sampleManager = sampleManager;
        }

        /// <summary>
        /// Sample - add a sample.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="tagsString">query string parameter</param>
        /// <returns></returns>
        [HttpPost("/api/ml/sample")]
        [DisableRequestSizeLimit]
        public Task<InvokeResult<Sample>> UploadSampleAsync (IFormFile file, string tagsString)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            if(String.IsNullOrEmpty(tagsString))
            {
                throw new ArgumentNullException("Must pass in ?tags as a comma delimted set of non empty tags.");
            }

            var tagIds = new List<string>( tagsString.Split(','));
            using (var stream = file.OpenReadStream())
            {
                var sample = new byte[stream.Length];
                stream.Position = 0;
                stream.ReadExactly(sample, 0, (int)stream.Length);

                return _sampleManager.AddSampleAsync(sample, file.FileName, file.ContentType, tagIds, OrgEntityHeader, UserEntityHeader);
            }
        }

        /// <summary>
        /// Sample - update sample content.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="sampleid"></param>
        /// <returns></returns>
        [HttpPut("/api/ml/sample/{sampleid}")]
        public Task<InvokeResult> UpdateSampleAsync(IFormFile file, string sampleid)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            using (var stream = file.OpenReadStream())
            {
                var sample = new byte[stream.Length];
                stream.Position = 0;
                stream.ReadExactly(sample, 0, (int)stream.Length);

                return _sampleManager.UpdateSampleAsync(sampleid, sample, OrgEntityHeader, UserEntityHeader);
            }
        }

        /// <summary>
        /// Sample - add a label for a sample.
        /// </summary>
        /// <param name="sampleid"></param>
        /// <param name="labelid"></param>
        /// <returns></returns>
        [HttpGet("/api/ml/sample/{sampleid}/label/{labelid}")]
        public Task<InvokeResult> AddLabelToSample(string sampleid, string labelid)
        {
            return _sampleManager.AddLabelForSampleAsync(sampleid, labelid, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Sample - remove label for sample.
        /// </summary>
        /// <param name="sampleid"></param>
        /// <param name="labelid"></param>
        /// <returns></returns>
        [HttpDelete("/api/ml/sample/{sampleid}/label/{labelid}")]
        public Task<InvokeResult> RemoveLabelFromSample(string sampleid, string labelid)
        {
            return _sampleManager.RemoveLabelFromSampleAsync(sampleid, labelid, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Sample - Get sample by id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/ml/sample/{id}")]
        public async Task<IActionResult> GetSampleAsync(string id)
        {
            var sampleDetail = await _sampleManager.GetSampleDetailAsync(id, OrgEntityHeader, UserEntityHeader);
            var result = await _sampleManager.GetSampleAsync(id, OrgEntityHeader, UserEntityHeader);

            var ms = new MemoryStream(result.Result);
            return new FileStreamResult(ms, sampleDetail.ContentType);
        }

        /// <summary>
        /// Sample - Get sample detail by id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/ml/sample/{id}/detail")]
        public Task<SampleDetail> GetSampleDetailsAsync(string id)
        {
            return _sampleManager.GetSampleDetailAsync(id, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Samples - get for label
        /// </summary>
        /// <param name="labelid"></param>
        /// <returns></returns>
        [HttpGet("/api/ml/samples/label/{labelid}")]
        public Task<ListResponse<SampleSummary>> GetSamplesForLabelAsync(string labelid)
        {
            if(!Request.Headers.ContainsKey("Accept"))
            {
                throw new ArgumentNullException("must provide content type in accept header.");
            }

            var contentType = Request.Headers["Accept"];

            return _sampleManager.GetSamplesForLabelAsync(labelid, contentType, OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }
    }
}
