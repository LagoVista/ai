using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface ILabelManager
    {
        Task<InvokeResult> AddLabelAsync(Label label, EntityHeader org, EntityHeader user);
        Task<InvokeResult> UpdateLabelAsync(Label label, EntityHeader org, EntityHeader user);
        Task<Label> GetLabelAsync(string id, EntityHeader org, EntityHeader user);
        Task<ListResponse<LabelSummary>> GetLabelsForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest);
        Task<ListResponse<LabelSummary>> SearchLabelsAsync(string search, EntityHeader org, EntityHeader user, ListRequest listRequest);
    }
}
