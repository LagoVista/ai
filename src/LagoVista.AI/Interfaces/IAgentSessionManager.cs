using System.Collections.Generic;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentSessionManager
    {
        Task AddAgentSessionAsync(AgentSession session, EntityHeader org, EntityHeader user);

        Task AddAgentSessionTurnAsync(string agentSessionId, AgentSessionTurn turn, EntityHeader org, EntityHeader user);

        Task SetRequestBlobUriAsync(string agentSessionid, string turnId, string requestBlobUri, EntityHeader org, EntityHeader user);

        Task CompleteAgentSessionTurnAsync(string agentSessionId, string turnId, string answerSummary, string answerBlobUrl, string openAiResponseId,
            int promptTokens, int completionTokens, int totalTokens,
            double executionMs, List<string> warnings, EntityHeader org, EntityHeader user);

        Task FailAgentSessionTurnAsync(string agentSessionId, string turnId, string openAiResponseId, double executionMs,
            List<string> errors, List<string> warnings, EntityHeader org, EntityHeader user);

        Task<AgentSession> GetAgentSessionAsync(string agentSessionId, EntityHeader org, EntityHeader user);

        Task<AgentSessionTurn> GetAgentSessionTurnAsync(string agentSessionId, string turnId, EntityHeader org,
            EntityHeader user);

        Task<AgentSessionTurn> GetLastAgentSessionTurnAsync(string agentSessionId, EntityHeader org, EntityHeader user);

        Task<ListResponse<AgentSessionSummary>> GetAgentSessionsAsync(ListRequest listRequest, EntityHeader org,
            EntityHeader user);

        Task<ListResponse<AgentSessionSummary>> GetAgentSessionsForUserAsync(string userId, ListRequest listRequest,
            EntityHeader org, EntityHeader user);

        Task<InvokeResult> SetSessionModeAsync(string sessionId, string mode, string reason, EntityHeader org, EntityHeader user);
    
        Task<InvokeResult> AbortTurnAsync(string sessionId, string turnId, EntityHeader org, EntityHeader user);

        Task<InvokeResult<AgentSessionSummary>> SetSessionNameAsync(string sessionid, string name, EntityHeader org, EntityHeader user);
        Task<InvokeResult<AgentSessionSummary>> ShareSessionAsync(string sessionid, EntityHeader org, EntityHeader user);
       
        Task<InvokeResult<AgentSessionSummary>> ArchiveSessionAsync(string sessionid, EntityHeader org, EntityHeader user);
        Task<InvokeResult<AgentSessionSummary>> DeleteSessionAsync(string sessionid, EntityHeader org, EntityHeader user);
        Task<InvokeResult<AgentSessionSummary>> CompleteSessionAsync(string sessionId, EntityHeader org, EntityHeader user);

        Task<InvokeResult<AgentSession>> BranchSessionAsync(string sessionId, string turnId, EntityHeader org, EntityHeader user);
    }
}
