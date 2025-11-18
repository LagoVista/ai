using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Stores and retrieves full logical request/response transcripts
    /// for a given org/session/turn. The session/turn records can store
    /// only summary fields plus blob URLs, while the full content lives here.
    /// </summary>
    public interface IAgentTurnTranscriptStore
    {
        /// <summary>
        /// Persist the full logical request payload for a turn and return
        /// an opaque blob URL or identifier that can be stored on the turn.
        /// </summary>
        Task<InvokeResult<System.Uri>> SaveTurnRequestAsync(AgentContext ctx, string orgId, string sessionId, string turnId, string requestJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// Persist the full logical response payload for a turn and return
        /// an opaque blob URL or identifier that can be stored on the turn.
        /// </summary>
        Task<InvokeResult<System.Uri>> SaveTurnResponseAsync(AgentContext ctx, string orgId, string sessionId, string turnId, string responseJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// Load the previously stored logical request payload for a turn.
        /// Returns null if no transcript was stored.
        /// </summary>
        Task<InvokeResult<string>> LoadTurnRequestAsync(AgentContext ctx, string orgId, string sessionId, string turnId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Load the previously stored logical response payload for a turn.
        /// Returns null if no transcript was stored.
        /// </summary>
        Task<InvokeResult<string>> LoadTurnResponseAsync(AgentContext ctx, string orgId, string sessionId, string turnId, CancellationToken cancellationToken = default);
    }
}
