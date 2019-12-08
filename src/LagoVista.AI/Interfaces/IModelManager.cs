using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IModelManager
    {
        Task<InvokeResult<ModelRevision>> AddRevisionAsync(String modelId, ModelRevision revision, EntityHeader org, EntityHeader user);
        Task<InvokeResult> AddModelAsync(Model model, EntityHeader org, EntityHeader user);
        Task<Model> GetModelAsync(string id, EntityHeader org, EntityHeader user);
        Task<InvokeResult> UpdateModelAsync(Model model, EntityHeader org, EntityHeader user);
        Task<DependentObjectCheckResult> CheckInUseAsync(string id, EntityHeader org, EntityHeader user);
        Task<InvokeResult> DeleteModelsync(string id, EntityHeader org, EntityHeader user);
        Task<bool> QueryKeyInUse(string key, EntityHeader org);
        Task<ListResponse<ModelSummary>> GetModelsForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest);
        Task<ListResponse<ModelSummary>> GetModelsForCategoryAsync(string categoryKey, EntityHeader org, EntityHeader user, ListRequest listRequest);
        Task<InvokeResult> UploadModel(string modelId, int revision, byte[] model, EntityHeader org, EntityHeader user);
        Task<InvokeResult<Byte[]>> GetMLModelAsync(string id, int revision, EntityHeader org, EntityHeader user);
    }
}
