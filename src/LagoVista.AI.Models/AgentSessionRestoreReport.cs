using System;
using LagoVista.Core;
using LagoVista.Core.Models;

namespace LagoVista.AI.Models
{
    public class AgentSessionRestoreReport
    {
        /// <summary>
        /// Internal persistence id.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToId();

        /// <summary>
        /// Short, stable, user-facing restore operation id (e.g. RST-0003).
        /// </summary>
        public string RestoreOperationId { get; set; }

        /// <summary>
        /// UTC timestamp when the restore operation started.
        /// </summary>
        public string StartedUtc { get; set; }

        /// <summary>
        /// UTC timestamp when the restore operation completed.
        /// </summary>
        public string CompletedUtc { get; set; }

        /// <summary>
        /// Source session id that was restored from.
        /// </summary>
        public string SourceSessionId { get; set; }

        /// <summary>
        /// Source checkpoint id that was restored (e.g., CP-0007).
        /// </summary>
        public string SourceCheckpointId { get; set; }

        /// <summary>
        /// Turn source id used as the branch anchor.
        /// </summary>
        public string SourceTurnSourceId { get; set; }

        /// <summary>
        /// Newly created branched session id.
        /// </summary>
        public string BranchedSessionId { get; set; }

        /// <summary>
        /// Copy counts captured for diagnostics.
        /// </summary>
        public int TurnsCopiedCount { get; set; }

        public int MemoryNotesCopiedCount { get; set; }

        public int CheckpointsCopiedCount { get; set; }

        public int ActiveFileRefsCopiedCount { get; set; }

        public int ChunkRefsCopiedCount { get; set; }

        /// <summary>
        /// User who initiated restore.
        /// </summary>
        public EntityHeader CreatedByUser { get; set; }

        /// <summary>
        /// Conversation id at time of restore.
        /// </summary>
        public string ConversationId { get; set; }

        /// <summary>
        /// Optional diagnostic warnings/errors for troubleshooting.
        /// </summary>
        public string Summary { get; set; }

        public string Details { get; set; }
    }
}
