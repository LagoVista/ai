using System;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentSessionFactory
    {
        Task<AgentSession> CreateSession(AgentPipelineContext ctx);

        AgentSessionTurn CreateTurnForNewSession(AgentPipelineContext ctx, AgentSession session);

        AgentSessionTurn CreateTurnForExistingSession(AgentPipelineContext ctx, AgentSession session);
    }
}
