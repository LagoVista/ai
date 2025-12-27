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
    /// Client-side wrapper tool for applying a restore in the UI/extension.
    /// This tool is used to explicitly hand off to the client to switch sessions,
    /// clear previous_response_id usage, and confirm it applied the change.
    ///
    /// The server preflights and returns a payload; the client performs the side-effect.
    /// </summary>
    public sealed class ClientSessionRestoreApplyTool : IAgentTool
    {
        private readonly IAdminLogger _logger;

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => false;

        public const string ToolName = "client_session_restore_apply";

        public const string ToolUsageMetadata = "Client-side tool to apply a checkpoint restore in the UI. Use after session_checkpoint_restore. The client must switch to newSessionId and reset the conversation chain (drop previous_response_id), then confirm.";

        public const string ToolSummary = "used to apply restoring a check point within a session";

        public ClientSessionRestoreApplyTool(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private sealed class Args
        {
            public string NewSessionId { get; set; }
            public string RestoreTurnId { get; set; }
            public bool? ResetConversationChain { get; set; }
            public string RestoreOperationId { get; set; }
            public string Message { get; set; }
        }

        private sealed class Result
        {
            public string Kind { get; set; }
            public string NewSessionId { get; set; }
            public string RestoreTurnId { get; set; }
            public bool ResetConversationChain { get; set; }
            public string RestoreOperationId { get; set; }
            public string Message { get; set; }
            public string SessionId { get; set; }
        }
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);


        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson)) return Task.FromResult(InvokeResult<string>.FromError("client_session_restore_apply requires a non-empty arguments object."));
            if (context == null) return Task.FromResult(InvokeResult<string>.FromError("client_session_restore_apply requires a valid execution context."));

            try
            {
                var args = JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args();

                if (string.IsNullOrWhiteSpace(args.NewSessionId)) return Task.FromResult(InvokeResult<string>.FromError("client_session_restore_apply requires 'newSessionId'."));
                if (string.IsNullOrWhiteSpace(args.RestoreTurnId)) return Task.FromResult(InvokeResult<string>.FromError("client_session_restore_apply requires 'restoreTurnId'."));

                var payload = new Result
                {
                    Kind = "checkpoint_restore_apply",
                    NewSessionId = args.NewSessionId.Trim(),
                    RestoreTurnId = args.RestoreTurnId.Trim(),
                    ResetConversationChain = args.ResetConversationChain ?? true,
                    RestoreOperationId = string.IsNullOrWhiteSpace(args.RestoreOperationId) ? null : args.RestoreOperationId.Trim(),
                    Message = string.IsNullOrWhiteSpace(args.Message) ? "Apply session restore in the client." : args.Message,
                    SessionId = context?.SessionId
                };

                return Task.FromResult(InvokeResult<string>.Create(JsonConvert.SerializeObject(payload)));
            }
            catch (Exception ex)
            {
                _logger.AddException("[ClientSessionRestoreApplyTool_ExecuteAsync__Exception]", ex);
                return Task.FromResult(InvokeResult<string>.FromError("client_session_restore_apply failed to process arguments."));
            }
        }

        public static object GetSchema()
        {
            return new
            {
                type = "function",
                name = ToolName,
                description = "Client-side tool to apply a checkpoint restore by switching sessions and resetting the conversation chain.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        newSessionId = new { type = "string", description = "New branched session id to switch to." },
                        restoreTurnId = new { type = "string", description = "Restore anchor turn id in the new session." },
                        resetConversationChain = new { type = "boolean", description = "If true, drop previous_response_id on the next /responses call." },
                        restoreOperationId = new { type = "string", description = "Restore operation id for fetching report details later." },
                        message = new { type = "string", description = "Optional user-facing message." }
                    },
                    required = new[] { "newSessionId", "restoreTurnId" }
                }
            };
        }
    }
}
