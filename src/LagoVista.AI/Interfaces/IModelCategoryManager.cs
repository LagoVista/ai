﻿using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IModelCategoryManager
    {
        Task<InvokeResult> AddModelCategoryAsync(ModelCategory modelCategory, EntityHeader org, EntityHeader user);
        Task<ModelCategory> GetModelCategoryAsync(string id, EntityHeader org, EntityHeader user);
        Task<DependentObjectCheckResult> CheckInUseAsync(string id, EntityHeader org, EntityHeader user);
        Task<ListResponse<ModelCategorySummary>> GetModelCategoriesForOrgAsync(EntityHeader org, EntityHeader user, ListRequest request);
        Task<InvokeResult> UpdateModelCategoryAsync(ModelCategory modelCategory, EntityHeader org, EntityHeader user);
        Task<InvokeResult> DeleteModelCategoryAsync(string id, EntityHeader org, EntityHeader user);
        Task<bool> QueryKeyInUse(string key, EntityHeader org);
    }
}
