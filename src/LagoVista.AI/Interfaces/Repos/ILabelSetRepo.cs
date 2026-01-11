// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 2f7f0e7fcb78f98627444dfaa60aa72f4221383d1a2ba8926343b472a6c96515
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Repos
{
    public interface ILabelSetRepo
    {
        Task AddLabelAsync(ModelLabelSet label);
        Task UpdateLabelAsync(ModelLabelSet label);

        Task<ModelLabelSet> GetLabelSetAsync(string id);
        Task<ListResponse<ModelLabelSetSummary>> GetLabelSetsForOrgAsync(string orgId, ListRequest listRequest);
        Task<bool> QueryKeyInUseAsync(string key, string org);
        Task DeleteLabelSetAsync(string id);
    }
}
