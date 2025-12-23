using System;
using System.Collections.Generic;
using System.Linq;
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
    /// List checkpoints stored on the current session.
    /// </summary>
    public sealed class SessionCheckpointListTool : IAgentTool
    {
        private readonly IAdminLogger _logger;
        private readonly IAgentSessionManager _sessions;

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolName = "session_checkpoint_list";

        public const string ToolUsageMetadata = "List checkpoints for the current session. Use when the user asks to view checkpoints or before restoring one.";

        public SessionCheckpointListTool(IAdminLogger logger, IAgentSessionManager sessions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        }

        private sealed class Args
        {
            public int? Limit { get; set; }
        }

        private sealed class Item
        {
            public string CheckpointId { get; set; }
            public string Name { get; set; }
            public string Notes { get; set; }
            public string TurnSourceId { get; set; }
            public string CreationDate { get; set; }
        }

        private sealed class Result
        {
            public List<Item> Items { get; set; } = new List<Item>();
            public int Count { get; set; }
            public string SessionId { get; set; }
        }
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (context == null) return InvokeResult<string>.FromError("session_checkpoint_list requires a valid execution context.");
            if (string.IsNullOrWhiteSpace(context.SessionId)) return InvokeResult<string>.FromError("session_checkpoint_list requires a sessionId in the execution context.");

            try
            {
                var args = string.IsNullOrWhiteSpace(argumentsJson) ? new Args() : (JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args());
                var limit = args.Limit.HasValue && args.Limit.Value > 0 ? args.Limit.Value : 100;

                var list = await _sessions.ListSessionCheckpointsAsync(context.SessionId, limit, context.Org, context.User);
                if (!list.Successful) return InvokeResult<string>.FromInvokeResult(list);

                var items = (list.Model ?? new List<AgentSessionCheckpoint>()).Select(cp => new Item
                {
                    CheckpointId = cp.CheckpointId,
                    Name = cp.Name,
                    Notes = cp.Notes,
                    TurnSourceId = cp.TurnSourceId,
                    CreationDate = cp.CreationDate
                }).ToList();

                var payload = new Result
                {
                    Items = items,
                    Count = items.Count,
                    SessionId = context?.SessionId
                };

                return InvokeResult<string>.Create(JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                _logger.AddException("[SessionCheckpointListTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("session_checkpoint_list failed to process arguments.");
            }
        }

        public static object GetSchema()
        {
            return new
            {
                type = "function",
                name = ToolName,
                description = "List checkpoints stored on the current session.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        limit = new { type = "integer", description = "Optional max items to return (default 100)." }
                    },
                    required = new string[] { }
                }
            };
        }
    }
}
