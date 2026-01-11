// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 4229293964c7d217776eb82235be28933a6f247017e02b07b18beffa39ad85ed
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Managers
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
