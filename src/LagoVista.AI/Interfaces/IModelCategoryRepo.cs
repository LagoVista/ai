using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IModelCategoryRepo
    {
        Task AddModelCategoryAsync(ModelCategory modelCategory);
        Task UpdateModelCategoryAsync(ModelCategory modelCategory);
        Task<ModelCategory> GetModelCategoryAsync(string modelId);
        Task<ListResponse<ModelCategorySummary>> GetModelCategorySummariesForOrgAsync(string orgId, ListRequest listRequest);
        Task DeleteModelCategoryAsync(string id);
        Task<bool> QueryKeyInUseAsync(string key, string org);
    }
}
