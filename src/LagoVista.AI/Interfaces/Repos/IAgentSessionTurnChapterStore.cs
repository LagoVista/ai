using LagoVista.AI.Models;
using LagoVista.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Repos
{
    public interface IAgentSessionTurnChapterStore
    {
        Task<AgentSessionChapter> SaveAsync(AgentSession session, AgentSessionChapter chapter, IReadOnlyList<AgentSessionTurn> turns, EntityHeader user, CancellationToken ct = default);

        Task<IReadOnlyList<AgentSessionTurn>> LoadAsync(AgentSessionChapter archive, CancellationToken ct = default);
    
        Task UpdateAsync(AgentSessionChapter archive, AgentSession session, IReadOnlyList<AgentSessionTurn> turns, EntityHeader user, CancellationToken ct = default);
    }
}
