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
    /// Create a durable checkpoint for the current session at the current turn.
    ///
    /// Used when the user says: "Set a checkpoint for 'XYZ'".
    /// </summary>
    public sealed class SessionCheckpointSetTool : IAgentTool
    {
        private readonly IAdminLogger _logger;
        private readonly IAgentSessionManager _sessions;

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolName = "session_checkpoint_set";

        public const string ToolUsageMetadata = "Create a durable checkpoint for the current session at the current turn. Use when the user asks to set a checkpoint. Returns a short CheckpointId and summary.";

        public SessionCheckpointSetTool(IAdminLogger logger, IAgentSessionManager sessions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        }

        private sealed class Args
        {
            public string Name { get; set; }
            public string Notes { get; set; }
            public string TurnId { get; set; }
        }

        private sealed class Result
        {
            public string CheckpointId { get; set; }
            public string Name { get; set; }
            public string Notes { get; set; }
            public string TurnSourceId { get; set; }
            public string CreationDate { get; set; }
            public string ConversationId { get; set; }
            public string SessionId { get; set; }
        }

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson)) return InvokeResult<string>.FromError("session_checkpoint_set requires a non-empty arguments object.");
            if (context == null) return InvokeResult<string>.FromError("session_checkpoint_set requires a valid execution context.");
            if (string.IsNullOrWhiteSpace(context.SessionId)) return InvokeResult<string>.FromError("session_checkpoint_set requires a sessionId in the execution context.");

            try
            {
                var args = JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args();
                if (string.IsNullOrWhiteSpace(args.Name)) return InvokeResult<string>.FromError("session_checkpoint_set requires 'name'.");

                var checkpoint = new AgentSessionCheckpoint
                {
                    Name = args.Name.Trim(),
                    Notes = string.IsNullOrWhiteSpace(args.Notes) ? null : args.Notes,
                    TurnSourceId = context.CurrentTurnId,
                    ConversationId = context.Request.ConversationId,
                    CreationDate = DateTime.UtcNow.ToString("o"),
                    CreatedByUser = context?.User
                };

                var add = await _sessions.AddSessionCheckpointAsync(context.SessionId, checkpoint, context.Org, context.User);
                if (!add.Successful) return InvokeResult<string>.FromInvokeResult(add.ToInvokeResult());

                var stored = add.Result;

                var payload = new Result
                {
                    CheckpointId = stored?.CheckpointId,
                    Name = stored?.Name,
                    Notes = stored?.Notes,
                    TurnSourceId = stored?.TurnSourceId,
                    CreationDate = stored?.CreationDate ?? checkpoint.CreationDate,
                    ConversationId = context?.Request?.ConversationId,
                    SessionId = context?.SessionId
                };

                return InvokeResult<string>.Create(JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                _logger.AddException("[SessionCheckpointSetTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("session_checkpoint_set failed to process arguments.");
            }
        }

        public static object GetSchema()
        {
            return new
            {
                type = "function",
                name = ToolName,
                description = "Create a checkpoint for the current session at the current turn.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "Checkpoint label (e.g., 'Parser baseline')." },
                        notes = new { type = "string", description = "Optional notes about the checkpoint." },
                        turnId = new { type = "string", description = "Optional explicit turn id to associate (if known)." }
                    },
                    required = new[] { "name" }
                }
            };
        }
    }
}
