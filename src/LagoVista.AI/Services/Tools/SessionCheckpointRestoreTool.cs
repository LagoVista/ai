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
        public const string ToolSummary = "restore a session to a known check point";
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
            public string SessionId { get; set; }
            public string Message { get; set; }
        }

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException();


        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
                return InvokeResult<string>.FromError("session_checkpoint_restore requires a non-empty arguments object.");
            if (context == null)
                return InvokeResult<string>.FromError("session_checkpoint_restore requires a valid execution context.");
           
            try
            {
                var args = JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args();
                if (string.IsNullOrWhiteSpace(args.CheckpointId))
                    return InvokeResult<string>.FromError("session_checkpoint_restore requires 'checkpointId'.");


                var restored = await _sessions.RestoreSessionCheckpointAsync(context.Session, args.CheckpointId.Trim(), context.Envelope.Org, context.Envelope.User);
                if (!restored.Successful)
                    return InvokeResult<string>.FromInvokeResult(restored.ToInvokeResult());
                
                
                var newSession = restored.Result;
                if (newSession == null || string.IsNullOrWhiteSpace(newSession.Id))
                    return InvokeResult<string>.FromError("session_checkpoint_restore did not return a new session.");
                var payload = new Result
                {
                    Kind = "checkpoint_restore",
                    ResetConversationChain = true,
                    SourceSessionId = context.Session.Id,
                    NewSessionId = newSession.Id,
                    CheckpointId = args.CheckpointId.Trim(),
                    RestoreTurnId = newSession.SourceTurnSourceId,
                    RestoreOperationId = newSession.RestoreOperationId,
                    RestoredOnUtc = newSession.RestoredOnUtc,
                    SessionId = context.Session.Id,
                    Message = $"Restored checkpoint {args.CheckpointId.Trim()} into a new branched session."};
                return InvokeResult<string>.Create(JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                _logger.AddException("[SessionCheckpointRestoreTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("session_checkpoint_restore failed to process arguments.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Restore a checkpoint by branching the current session from the checkpoint's turn (hard restore).", p =>
            {
                p.String("checkpointId", "CheckpointId to restore (e.g., CP-0007).", required: true);
                p.String("notes", "Optional reason/notes for the restore.");
            });
        }
    }
}