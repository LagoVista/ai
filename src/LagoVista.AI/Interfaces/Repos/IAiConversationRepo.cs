// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: a9b9a8e71dce7b63db6a4560df0b258c21d8b00acddba959231ad7de0446223d
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Repos
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
