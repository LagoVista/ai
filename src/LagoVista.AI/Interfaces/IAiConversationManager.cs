using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI
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
