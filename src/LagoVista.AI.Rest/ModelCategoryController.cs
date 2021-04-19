using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Identity;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using LagoVista.Core.Validation;
using System;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.IoT.Web.Common.Attributes;

namespace LagoVista.AI.Rest
{
    /// <summary>
    /// REST Class for ML Categories
    /// </summary>
    [ConfirmedUser]
    [AppBuilder]
    public class ModelCategoryController : LagoVistaBaseController
    {
        readonly IModelCategoryManager _mgr;

        /// <summary>
        /// Construct ML Class for Categories
        /// </summary>
        /// <param name="mgr"></param>
        /// <param name="userManager"></param>
        /// <param name="logger"></param>
        public ModelCategoryController(IModelCategoryManager mgr, UserManager<AppUser> userManager, IAdminLogger logger)
            : base(userManager, logger)
        {
            _mgr = mgr;
        }

        /// <summary>
        /// Model Category - Add
        /// </summary>
        /// <param name="modelCategory"></param>
        /// <returns></returns>
        [HttpPost("/api/ml/modelcategory")]
        public Task<InvokeResult> AddInstanceAsync([FromBody] ModelCategory modelCategory)
        {
            return _mgr.AddModelCategoryAsync(modelCategory, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Model Category - Update
        /// </summary>
        /// <param name="modelCategory"></param>
        /// <returns></returns>
        [HttpPut("/api/ml/modelcategory")]
        public Task<InvokeResult> UpdateInstanceAsync([FromBody] ModelCategory modelCategory)
        {
            SetUpdatedProperties(modelCategory);
            return _mgr.UpdateModelCategoryAsync(modelCategory, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Model Category - Update
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/ml/modellabel/factory")]
        public DetailResponse<ModelLabel> CreateLabel()
        {
            var lbl = new ModelLabel();
            lbl.Id = Guid.NewGuid().ToId();

            return DetailResponse<ModelLabel>.Create(lbl);
        }

        /// <summary>
        /// Model Category - Delete
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("/api/ml/modelcategory/{id}")]
        public Task<InvokeResult> DeleteModelCategoryAsync(string id)
        {
            return _mgr.DeleteModelCategoryAsync(id, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Model Category - Get all for org
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/ml/modelcategories")]
        public Task<ListResponse<ModelCategorySummary>> GetModelCategoryForOrg()
        {
            return  _mgr.GetModelCategoriesForOrgAsync(OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }

        /// <summary>
        /// Model Category - Get Model Category
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/ml/modelcategory/{id}")]
        public async Task<DetailResponse<ModelCategory>> GetModelCategoryAsync(string id)
        {
            var modelCateogry = await _mgr.GetModelCategoryAsync(id, OrgEntityHeader, UserEntityHeader);
            return DetailResponse<ModelCategory>.Create(modelCateogry);
        }

        /// <summary>
        /// Model Category - Create new
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/ml/modelcategory/factory")]
        public DetailResponse<ModelCategory> CreateNewModelCategory()
        {
            var modelCategory = DetailResponse<ModelCategory>.Create();
            modelCategory.Model.Id = Guid.NewGuid().ToId();
            SetAuditProperties(modelCategory.Model);
            SetOwnedProperties(modelCategory.Model);
            return modelCategory;

        }

        /// <summary>
        /// Model Category - Key In Use
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/ml/modelcategory/{key}/keyinuse")]
        public Task<bool> ModelCategoryKeyInUseAsync(String key)
        {
            return _mgr.QueryKeyInUse(key, OrgEntityHeader);
        }
    }
}
