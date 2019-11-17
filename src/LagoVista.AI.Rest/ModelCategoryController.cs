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

namespace LagoVista.AI.Rest
{
    public class ModelCategoryController : LagoVistaBaseController
    {
        readonly IModelCategoryManager _mgr;

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
        [HttpPost("/api/modelcategory")]
        public Task<InvokeResult> AddInstanceAsync([FromBody] ModelCategory modelCategory)
        {
            return _mgr.AddModelCategoryAsync(modelCategory, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Model Category - Update
        /// </summary>
        /// <param name="modelCategory"></param>
        /// <returns></returns>
        [HttpPut("/api/modelcategory")]
        public Task<InvokeResult> UpdateInstanceAsync([FromBody] ModelCategory modelCategory)
        {
            SetUpdatedProperties(modelCategory);
            return _mgr.UpdateModelCategoryAsync(modelCategory, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Model Category - Delete
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("/api/modelcategory/{id}")]
        public Task<InvokeResult> DeleteModelCategoryAsync(string id)
        {
            return _mgr.DeleteModelCategoryAsync(id, OrgEntityHeader, UserEntityHeader);
        }

        /// <summary>
        /// Model Category - Get all for org
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/modelcategories")]
        public Task<ListResponse<ModelCategorySummary>> GetModelCategoryForOrg()
        {
            return  _mgr.GetModelCategoriesForOrgAsync(OrgEntityHeader.Id, UserEntityHeader, GetListRequestFromHeader());
        }

        /// <summary>
        /// Model Category - Get Model Category
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/modelcategory/{id}")]
        public async Task<DetailResponse<ModelCategory>> GetModelCategoryAsync(string id)
        {
            var modelCateogry = await _mgr.GetModelCategoryAsync(id, OrgEntityHeader, UserEntityHeader);
            return DetailResponse<ModelCategory>.Create(modelCateogry);
        }

        /// <summary>
        /// Model Category - Create new
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("/api/modelcategory/factory")]
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
        [HttpGet("/api/modelcategory/{key}/keyinuse")]
        public Task<bool> ModelCategoryKeyInUseAsync(String key)
        {
            return _mgr.QueryKeyInUse(key, OrgEntityHeader);
        }
    }
}
