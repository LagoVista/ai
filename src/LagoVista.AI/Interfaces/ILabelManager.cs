// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: c396360bee8f9b6f88ccf6a276b9237fb60fdabea9560817043e77ad22ff4e46
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface ILabelManager
    {
        Task<InvokeResult> AddLabelAsync(LagoVista.AI.Models.Label label, EntityHeader org, EntityHeader user);
        Task<InvokeResult> UpdateLabelAsync(Models.Label label, EntityHeader org, EntityHeader user);
        Task<Models.Label> GetLabelAsync(string id, EntityHeader org, EntityHeader user);
        Task<ListResponse<LabelSummary>> GetLabelsForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest);
        Task<ListResponse<LabelSummary>> SearchLabelsAsync(string search, EntityHeader org, EntityHeader user, ListRequest listRequest);


        Task<InvokeResult> AddLabelSetAsync(ModelLabelSet label, EntityHeader org, EntityHeader user);
        Task<InvokeResult> UpdateLabelSetAsync(ModelLabelSet label, EntityHeader org, EntityHeader user);
        Task<ModelLabelSet> GetLabelSetAsync(string id, EntityHeader org, EntityHeader user);
        Task<InvokeResult> DeleteLabelSetAsync(string id, EntityHeader org, EntityHeader user);
        Task<ListResponse<ModelLabelSetSummary>> GetLabelSetsForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest);
    
    }
}
