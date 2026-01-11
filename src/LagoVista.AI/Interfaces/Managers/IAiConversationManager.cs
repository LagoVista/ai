// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 2f04e2f98ad944f3de73e4c7292446c0ecea5cec15a26893fe0e87f14b517846
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Managers
{
    public interface IAiConversationManager
    {
        Task<InvokeResult> AddAiConversationAsync(AiConversation aiConversation, EntityHeader org, EntityHeader user);
        Task<AiConversation> GetAiConversationAsync(string id, EntityHeader org, EntityHeader user);
        Task<DependentObjectCheckResult> CheckInUseAsync(string id, EntityHeader org, EntityHeader user);
        Task<ListResponse<AiConversationSummary>> GetConversationsForOrgAsync(EntityHeader org, EntityHeader user, ListRequest request);
        Task<ListResponse<AiConversationSummary>> GetConversationsForUserAsync(string userId, EntityHeader org, EntityHeader user, ListRequest request);
        Task<InvokeResult> UpdateAiConversationAsync(AiConversation aiConversation, EntityHeader org, EntityHeader user);
        Task<InvokeResult> DeleteAiConversationAsync(string id, EntityHeader org, EntityHeader user);
        Task<bool> QueryKeyInUse(string key, EntityHeader org);
    }
}
