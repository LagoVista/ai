using System.Collections.Generic;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces.Managers
{
    public interface IAgentSessionManager
    {
        Task AddAgentSessionAsync(AgentSession session, EntityHeader org, EntityHeader user);

        Task<AgentSession> GetAgentSessionAsync(string agentSessionId, EntityHeader org, EntityHeader user);

        Task<ListResponse<AgentSessionSummary>> GetAgentSessionsForUserAsync(string userId, ListRequest listRequest, EntityHeader org, EntityHeader user);


        Task<InvokeResult<AgentSessionSummary>> SetSessionNameAsync(string sessionid, string name, EntityHeader org, EntityHeader user);

        Task<InvokeResult<AgentSessionSummary>> ShareSessionAsync(string sessionid, EntityHeader org, EntityHeader user);

        Task<InvokeResult<AgentSessionSummary>> ArchiveSessionAsync(string sessionid, EntityHeader org, EntityHeader user);

        Task<InvokeResult<AgentSessionSummary>> DeleteSessionAsync(string sessionid, EntityHeader org, EntityHeader user);

        Task<InvokeResult<AgentSessionSummary>> CompleteSessionAsync(string sessionId, EntityHeader org, EntityHeader user);

        Task<InvokeResult<AgentSession>> BranchSessionAsync(string sessionId, string turnId, EntityHeader org, EntityHeader user);


        // --- Session Checkpoints ---

        Task<InvokeResult<AgentSessionCheckpoint>> AddSessionCheckpointAsync(string sessionId, AgentSessionCheckpoint checkpoint, EntityHeader org, EntityHeader user);

        Task<ListResponse<AgentSessionCheckpoint>> ListSessionCheckpointsAsync(string sessionId, int limit, EntityHeader org, EntityHeader user);

        Task<InvokeResult<AgentSession>> RestoreSessionCheckpointAsync(AgentSession session, string checkpointId, EntityHeader org, EntityHeader user);


        Task<InvokeResult<AgentSession>> RollbackAsync(string sessionId, string turnId, EntityHeader org, EntityHeader user);


        Task<InvokeResult> UpdateSessionAsync(AgentSession session, EntityHeader org, EntityHeader user);
        Task<InvokeResult<AgentSessionArchive>> CheckpointAndResetAsync(AgentSession session, string chapterTitle, EntityHeader org, EntityHeader user);

    }
}
