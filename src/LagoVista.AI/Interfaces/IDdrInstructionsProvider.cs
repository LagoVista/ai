using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Models;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// AGN-018: Provides per-mode DDR instruction text suitable for the OpenAI Responses API "instructions" field.
    ///
    /// This service is responsible for ensuring the AgentSession.DdrCache contains an entry for the requested mode.
    /// Sessions are short-lived; cache invalidation within a running session is not required.
    /// </summary>
    public interface IDdrInstructionsProvider
    {
        /// <summary>
        /// Ensures that session.DdrCache contains a cached instruction string for the provided mode.
        /// If the cache already contains the mode key, this method is a no-op.
        /// </summary>
        Task EnsureModeInstructionsCachedAsync(
            AgentSession session,
            AgentMode mode,
            EntityHeader org,
            EntityHeader user);

        /// <summary>
        /// Gets cached DDR instruction text for the specified mode key, if present.
        /// Returns null if no cache entry exists.
        /// </summary>
        string GetCachedModeInstructions(AgentSession session, string modeKey);
    }
}
