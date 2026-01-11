// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: b034d258a98d86c2882ab398e5145c0a69d30935ed686de5f005a479600cf5c1
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Repos
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
