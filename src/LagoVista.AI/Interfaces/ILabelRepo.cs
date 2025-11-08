// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: e4f15702e2f39a5438c2d64bea48c343d3d0f55b6cc72b0f4c954c5ff9a6a2dd
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface ILabelRepo
    {
        Task AddLabelAsync(Label label);
        Task UpdateLabelAsync(Label label);
      
        Task<Label> GetLabelAsync(string id);
        Task<ListResponse<LabelSummary>> SearchLabelsForOrgAsync(string orgId, string searchString, ListRequest listRequest);
        Task<ListResponse<LabelSummary>> GetLabelsForOrgAsync(string orgId, ListRequest listRequest);
        Task<bool> QueryKeyInUseAsync(string key, string org);
    }
}
