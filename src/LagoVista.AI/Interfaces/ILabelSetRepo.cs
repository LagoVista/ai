using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI
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
