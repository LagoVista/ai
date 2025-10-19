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
