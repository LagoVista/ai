// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: e4f15702e2f39a5438c2d64bea48c343d3d0f55b6cc72b0f4c954c5ff9a6a2dd
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Repos
{
    public interface ILabelRepo
    {
        Task AddLabelAsync(AiModelLabel label);
        Task UpdateLabelAsync(AiModelLabel label);

        Task<AiModelLabel> GetLabelAsync(string id);
        Task<ListResponse<AiModelLabelSummary>> SearchLabelsForOrgAsync(string orgId, string searchString, ListRequest listRequest);
        Task<ListResponse<AiModelLabelSummary>> GetLabelsForOrgAsync(string orgId, ListRequest listRequest);
        Task<bool> QueryKeyInUseAsync(string key, string org);
    }
}
