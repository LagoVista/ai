using System.Threading.Tasks;
using LagoVista.AI.Models;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentSessionFactory
    {
        Task<AgentSession> CreateSession(IAgentPipelineContext ctx);

        AgentSessionTurn CreateTurnForNewSession(IAgentPipelineContext ctx, AgentSession session);

        AgentSessionTurn CreateTurnForExistingSession(IAgentPipelineContext ctx, AgentSession session);
        AgentSessionTurn CreateTurnForNewChapter(IAgentPipelineContext ctx, AgentSession session);
    }
}
