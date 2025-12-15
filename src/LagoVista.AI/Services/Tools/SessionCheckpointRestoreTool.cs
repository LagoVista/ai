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
    /// Restore a checkpoint by branching the session from the checkpoint's referenced turn.
    /// </summary>
    public sealed class SessionCheckpointRestoreTool : IAgentTool
    {
        private readonly IAdminLogger _logger;
        private readonly IAgentSessionManager _sessions;

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolName = "session_checkpoint_restore";

        public const string ToolUsageMetadata = "Restore a checkpoint by branching the session from the checkpoint's turn. Use when the user asks to restore a checkpoint.";

        public SessionCheckpointRestoreTool(IAdminLogger logger, IAgentSessionManager sessions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        }

        private sealed class Args
        {
            public string CheckpointId { get; set; }
        }

        private sealed class Result
        {
            public string RequestedCheckpointId { get; set; }
            public string RestoredSessionId { get; set; }
            public string ConversationId { get; set; }
            public string SessionId { get; set; }
        }

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson)) return InvokeResult<string>.FromError("session_checkpoint_restore requires a non-empty arguments object.");
            if (context == null) return InvokeResult<string>.FromError("session_checkpoint_restore requires a valid execution context.");
            if (string.IsNullOrWhiteSpace(context.SessionId)) return InvokeResult<string>.FromError("session_checkpoint_restore requires a sessionId in the execution context.");

            try
            {
                var args = JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args();
                if (string.IsNullOrWhiteSpace(args.CheckpointId)) return InvokeResult<string>.FromError("session_checkpoint_restore requires 'checkpointId' (e.g., CP-0007).");

                var restore = await _sessions.RestoreSessionCheckpointAsync(context.SessionId, args.CheckpointId.Trim(), context.Org, context.User);
                if (!restore.Successful) return InvokeResult<string>.FromInvokeResult(restore.ToInvokeResult());

                var restoredSession = restore.Result;

                var payload = new Result
                {
                    RequestedCheckpointId = args.CheckpointId.Trim(),
                    RestoredSessionId = restoredSession?.Id,
                    ConversationId = context?.Request?.ConversationId,
                    SessionId = context?.SessionId
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
                description = "Restore a checkpoint by branching the session from that checkpoint's turn.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        checkpointId = new { type = "string", description = "Checkpoint id to restore (e.g., CP-0007)." }
                    },
                    required = new[] { "checkpointId" }
                }
            };
        }
    }
}
