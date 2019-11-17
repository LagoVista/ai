using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IModelRepo
    {
        Task AddModelAsync(Model model);
        Task UpdateModelAsync(Model model);
        Task<Model> GetModelAsync(string modelId);
        Task<ListResponse<ModelSummary>> GetModelSummariesForCategoryAsync(string orgId, string categoryId, ListRequest listRequest);
        Task<ListResponse<ModelSummary>> GetModelSummariesForOrgAsync(string orgId, ListRequest listRequest);
        Task DeleteModelAsync(string id);
        Task<bool> QueryKeyInUseAsync(string key, string org);
    }
}
