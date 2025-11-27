using System;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentSessionFactory
    {
        Task<AgentSession> CreateSession(AgentExecuteRequest request, AgentContext context, OperationKinds operationKind, EntityHeader org, EntityHeader user);

        AgentSessionTurn CreateTurnForNewSession(AgentSession session, AgentExecuteRequest request, EntityHeader org, EntityHeader user);

        AgentSessionTurn CreateTurnForExistingSession(AgentSession session, AgentExecuteRequest request, EntityHeader org, EntityHeader user);
    }
}
