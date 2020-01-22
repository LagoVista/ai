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
        Task<ListResponse<LabelSummary>> GetLabelsForOrgAsync(string orgId);
        Task<bool> QueryKeyInUseAsync(string key, string org);

    }
}
