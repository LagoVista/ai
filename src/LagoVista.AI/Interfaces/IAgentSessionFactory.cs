using System;
using LagoVista.AI.Models;
using LagoVista.Core.Models;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentSessionFactory
    {
        AgentSession CreateSession(NewAgentExecutionSession request, EntityHeader org, EntityHeader user);

        AgentSessionTurn CreateTurnForNewSession(AgentSession session, NewAgentExecutionSession request, EntityHeader org, EntityHeader user);

        AgentSessionTurn CreateTurnForExistingSession(AgentSession session, AgentExecutionRequest request, EntityHeader org, EntityHeader user);
    }
}
