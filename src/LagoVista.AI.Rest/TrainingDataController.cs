﻿using LagoVista.AI.Models.TrainingData;
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
    [ConfirmedUser]
    [AppBuilder]
    public class TrainingDataController : LagoVistaBaseController
    {
        readonly ITrainingDataSetManager _mgr;

        public TrainingDataController(ITrainingDataSetManager mgr, UserManager<AppUser> userManager, IAdminLogger logger)
            : base(userManager, logger)
        {
            _mgr = mgr;
        }

        /// <summary>
        /// Training Dataset - add
        /// </summary>
        /// <param name="set"></param>
        /// <returns></returns>
        [HttpPost("/api/ml/trainingdataset")]
        public Task<InvokeResult> AddTrainingDataSetAsync([FromBody] TrainingDataSet set)
        {
            return this._mgr.AddTrainingDataSetManager(set, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Trianing Dataset - update
        /// </summary>
        /// <param name="set"></param>
        /// <returns></returns>
        [HttpPut("/api/ml/trainingdataset")]
        public Task<InvokeResult> UpdateTrainingDataSetAsync([FromBody] TrainingDataSet set)
        {
            this.SetUpdatedProperties(set);
            return this._mgr.UpdateTrainingDataSetManager(set, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Training Dataset - get by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/ml/trainingdataset/{id}")]
        public async Task<DetailResponse<TrainingDataSet>> GetLabelAsync(string id)
        {
            return DetailResponse<TrainingDataSet>.Create(await _mgr.GetTrainingDataSetAsync(id, OrgEntityHeader, UserEntityHeader));
        }

        /// <summary>
        /// Training Dataset - get by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/ml/trainingdataset/factory")]
        public DetailResponse<TrainingDataSet> TrainingSetFactory()
        {
            var result = DetailResponse<TrainingDataSet>.Create();
            result.Model.Id = Guid.NewGuid().ToId();
            this.SetOwnedProperties(result.Model);
            this.SetAuditProperties(result.Model);
            return result;
        }

        /// <summary>
        /// Training Dataset - delete by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("/api/ml/trainingdataset/{id}")]
        public async Task<DetailResponse<TrainingDataSet>> DeleteLabelAsync(string id)
        {
            return DetailResponse<TrainingDataSet>.Create(await _mgr.GetTrainingDataSetAsync(id, OrgEntityHeader, UserEntityHeader));
        }

        /// <summary>
        /// Training Datasets - get for org
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/ml/trainingdatasets")]
        public Task<ListResponse<TrainingDataSetSummary>> GetLabelsForOrgAsync()
        {
            return _mgr.GetForOrgAsync(OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }
    }
}
