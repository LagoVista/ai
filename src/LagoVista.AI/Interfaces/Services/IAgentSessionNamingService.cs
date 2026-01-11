using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;

namespace LagoVista.AI.Interfaces.Services
{
    /// <summary>
    /// Provides short human-readable names for new Aptix sessions
    /// based on the user instruction.
    /// </summary>
    public interface IAgentSessionNamingService
    {
        /// <summary>
        /// Generate a short summary name for a new session.
        /// Returned string must be <= 60 characters and contain
        /// plain words suitable for EntityBase.Name.
        /// </summary>
        Task<string> GenerateNameAsync(
            AgentContext agentContext,
            string instruction,
            CancellationToken cancellationToken);
    }
}
