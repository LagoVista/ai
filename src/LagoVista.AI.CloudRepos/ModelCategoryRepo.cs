using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;

namespace LagoVista.AI.CloudRepos
{
    public class ModelCategoryRepo : IModelCategoryRepo
    {
        public Task AddModelCategoryAsync(ModelCategory modelCategory)
        {
            throw new NotImplementedException();
        }

        public Task DeleteModelCategoryAsync(string id)
        {
            throw new NotImplementedException();
        }

        public Task<ModelCategory> GetModelCategoryAsync(string modelId)
        {
            throw new NotImplementedException();
        }

        public Task<ListResponse<ModelCategorySummary>> GetModelCategorySummariesForOrgAsync(string orgId, ListRequest listRequest)
        {
            throw new NotImplementedException();
        }

        public Task<bool> QueryKeyInUseAsync(string key, string org)
        {
            throw new NotImplementedException();
        }

        public Task UpdateModelCategoryAsync(ModelCategory modelCategory)
        {
            throw new NotImplementedException();
        }
    }
}
