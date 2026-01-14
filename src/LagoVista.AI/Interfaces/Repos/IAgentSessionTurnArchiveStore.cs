using LagoVista.AI.Models;
using LagoVista.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Repos
{
    public interface IAgentSessionTurnArchiveStore
    {
        Task<AgentSessionArchive> SaveAsync(AgentSession session, IReadOnlyList<AgentSessionTurn> turns, string title, string summary, EntityHeader user, CancellationToken ct = default);

        Task<IReadOnlyList<AgentSessionTurn>> LoadAsync(AgentSessionArchive archive, CancellationToken ct = default);
    }
}
