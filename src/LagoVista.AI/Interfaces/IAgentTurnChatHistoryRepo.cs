using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentTurnChatHistoryRepo
    {
        Task AppendTurnAsync(string orgId,
        string sessionId,
        string turnId,
        string userInstructions,
        string modelResponseText,
        CancellationToken ct = default);

		Task<ListResponse<AgentTurnChatHistory>> RestoreSessionAsync(string orgId, string sessionId, CancellationToken ct);
	}
}
