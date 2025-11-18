using System;

namespace LagoVista.AI.Models
{
    /// <summary>
    /// Lightweight event payload published by the Aptix orchestrator over notifications
    /// (e.g. WebSockets) to keep clients informed of progress.
    /// </summary>
    public class AptixOrchestratorEvent
    {
        /// <summary>
        /// Agent session identifier this event belongs to.
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Turn identifier within the session (may be null for very early events).
        /// </summary>
        public string TurnId { get; set; }

        /// <summary>
        /// High-level stage name, e.g. TurnCreated, RagStarted, RagCompleted,
        /// LlmStarted, LlmCompleted, TurnCompleted, TurnFailed, TurnAborted.
        /// </summary>
        public string Stage { get; set; }

        /// <summary>
        /// Optional status snapshot, typically pending | completed | failed | aborted.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Short human-readable message suitable for direct display in the UI.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Optional elapsed time in milliseconds for the stage, if known.
        /// </summary>
        public double? ElapsedMs { get; set; }

        /// <summary>
        /// Optional UTC ISO-8601 timestamp when this event was emitted.
        /// </summary>
        public string Timestamp { get; set; }
    }
}
