using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Restore a previously created checkpoint by branching the current session
    /// from the checkpoint's anchor turn. This is the "hard restore" path used
    /// to reset the LLM conversation chain (drop previous_response_id).
    ///
    /// Typical user utterance: "Restore checkpoint CP-0007".
    /// </summary>
    public sealed class SessionCheckpointRestoreTool : IAgentTool
    {
        private readonly IAdminLogger _logger;
        private readonly IAgentSessionManager _sessions;

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolName = "session_checkpoint_restore";

        public const string ToolUsageMetadata = "Restore a checkpoint by branching the session to the checkpoint's turn. Use when the user asks to restore a checkpoint to reset context. Returns the new SessionId plus restore info for the client.";

        public SessionCheckpointRestoreTool(IAdminLogger logger, IAgentSessionManager sessions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        }

        private sealed class Args
        {
            public string CheckpointId { get; set; }
            public string Notes { get; set; }
        }

        private sealed class Result
        {
            public string Kind { get; set; }
            public bool ResetConversationChain { get; set; }
            public string SourceSessionId { get; set; }
            public string NewSessionId { get; set; }
            public string CheckpointId { get; set; }
            public string RestoreTurnId { get; set; }
            public string RestoreOperationId { get; set; }
            public string RestoredOnUtc { get; set; }
            public string ConversationId { get; set; }
            public string SessionId { get; set; }
            public string Message { get; set; }
        }
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson)) return InvokeResult<string>.FromError("session_checkpoint_restore requires a non-empty arguments object.");
            if (context == null) return InvokeResult<string>.FromError("session_checkpoint_restore requires a valid execution context.");
            if (string.IsNullOrWhiteSpace(context.SessionId)) return InvokeResult<string>.FromError("session_checkpoint_restore requires a sessionId in the execution context.");

            try
            {
                var args = JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args();
                if (string.IsNullOrWhiteSpace(args.CheckpointId)) return InvokeResult<string>.FromError("session_checkpoint_restore requires 'checkpointId'.");

                var sourceSessionId = context.SessionId;

                var restored = await _sessions.RestoreSessionCheckpointAsync(sourceSessionId, args.CheckpointId.Trim(), context.Org, context.User);
                if (!restored.Successful) return InvokeResult<string>.FromInvokeResult(restored.ToInvokeResult());

                var newSession = restored.Result;
                if (newSession == null || string.IsNullOrWhiteSpace(newSession.Id)) return InvokeResult<string>.FromError("session_checkpoint_restore did not return a new session.");

                var payload = new Result
                {
                    Kind = "checkpoint_restore",
                    ResetConversationChain = true,
                    SourceSessionId = sourceSessionId,
                    NewSessionId = newSession.Id,
                    CheckpointId = args.CheckpointId.Trim(),
                    RestoreTurnId = newSession.SourceTurnSourceId,
                    RestoreOperationId = newSession.RestoreOperationId,
                    RestoredOnUtc = newSession.RestoredOnUtc,
                    ConversationId = context?.Request?.ConversationId,
                    SessionId = context?.SessionId,
                    Message = $"Restored checkpoint {args.CheckpointId.Trim()} into a new branched session."
                };

                return InvokeResult<string>.Create(JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                _logger.AddException("[SessionCheckpointRestoreTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("session_checkpoint_restore failed to process arguments.");
            }
        }

        public static object GetSchema()
        {
            return new
            {
                type = "function",
                name = ToolName,
                description = "Restore a checkpoint by branching the current session from the checkpoint's turn (hard restore).",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        checkpointId = new { type = "string", description = "CheckpointId to restore (e.g., CP-0007)." },
                        notes = new { type = "string", description = "Optional reason/notes for the restore." }
                    },
                    required = new[] { "checkpointId" }
                }
            };
        }
    }
}
