using System.Threading.Tasks;
using LagoVista.AI.Models;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentSessionFactory
    {
        Task<AgentSession> CreateSession(IAgentPipelineContext ctx);

        AgentSessionTurn CreateTurnForNewSession(IAgentPipelineContext ctx, AgentSession newSession);

        AgentSessionTurn CreateTurnForExistingSession(IAgentPipelineContext ctx, AgentSession existingSession);
        AgentSessionTurn CreateTurnForNewChapter(IAgentPipelineContext ctx);
        AgentSessionTurn CreateFirstTurnForNewChapter(IAgentPipelineContext ctx, AgentSession existingSessoin);

        AgentSessionChapter CreateBoundaryTurnForNewChapter(IAgentPipelineContext ctx);
    }
}
