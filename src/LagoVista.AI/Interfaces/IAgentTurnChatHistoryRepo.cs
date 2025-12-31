using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentTurnChatHistoryRepo
    {
        Task AddTurnAsync(string orgId, string sessionId, string turnId, string userInstructions, string modelResponseText);

        Task<AgentTurnChatHistory> GetTurnAsync(string orgId, string sessionId, string turnId);

		Task<ListResponse<AgentTurnChatHistory>> GetTurnsAsync(string orgId, string sessionId);
	}
}
