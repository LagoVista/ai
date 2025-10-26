using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IAiConversationRepo
    {
        Task AddAiConversationAsync(AiConversation aiConversation);
        Task UpdateAiConversationAsync(AiConversation aiConversation);
        Task<AiConversation> GetAiConversationAsync(string modelId);
        Task<ListResponse<AiConversationSummary>> GetAiConversationSummariesForOrgAsync(string orgId, ListRequest listRequest);
        Task<ListResponse<AiConversationSummary>> GetAiConversationSummariesForUserAsync(string orgId, string userId, ListRequest listRequest);
        Task DeleteAiConversationAsync(string id);
        Task<bool> QueryKeyInUseAsync(string key, string org);
    }
}
