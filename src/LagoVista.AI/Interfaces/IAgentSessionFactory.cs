using System;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Models;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentSessionFactory
    {
        Task<AgentSession> CreateSession(NewAgentExecutionSession request, AgentContext context, EntityHeader org, EntityHeader user);

        AgentSessionTurn CreateTurnForNewSession(AgentSession session, NewAgentExecutionSession request, EntityHeader org, EntityHeader user);

        AgentSessionTurn CreateTurnForExistingSession(AgentSession session, AgentExecutionRequest request, EntityHeader org, EntityHeader user);
    }
}
