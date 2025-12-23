using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Stores a durable "memory note" on the current agent session (invariant/decision/constraint/etc.).
    ///
    /// Intended to be called only when the user explicitly says: remember this / write it down / save this.
    /// </summary>
    public sealed class SessionMemoryStoreTool : IAgentTool
    {
        private readonly IAdminLogger _logger;
        private readonly IAgentSessionManager _agentSessionManager;

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = "Store a durable session memory note only when the user explicitly asks to remember/save/write something down. Returns a short MemoryId and a one-line summary of what was stored.";

        public const string ToolName = "session_memory_store";

        public SessionMemoryStoreTool(IAdminLogger logger, IAgentSessionManager agentSessionManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _agentSessionManager = agentSessionManager ?? throw new ArgumentNullException(nameof(agentSessionManager));
        }

        private sealed class StoreArgs
        {
            public string Title { get; set; }
            public string Summary { get; set; }
            public string Details { get; set; }
            public List<string> Tags { get; set; }
            public string Importance { get; set; }
            public string Kind { get; set; }
        }

        private sealed class StoreResult
        {
            public string MemoryId { get; set; }
            public string Title { get; set; }
            public string Summary { get; set; }
            public string Kind { get; set; }
            public string Importance { get; set; }
            public List<string> Tags { get; set; }
            public string ConversationId { get; set; }
            public string SessionId { get; set; }
            public string TurnSourceId { get; set; }
            public string CreationDate { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return InvokeResult<string>.FromError("session_memory_store requires a non-empty arguments object.");
            }

            if (context == null)
            {
                return InvokeResult<string>.FromError("session_memory_store requires a valid execution context.");
            }

            if (string.IsNullOrWhiteSpace(context.SessionId))
            {
                return InvokeResult<string>.FromError("session_memory_store requires a sessionId in the execution context.");
            }

            try
            {
                var args = JsonConvert.DeserializeObject<StoreArgs>(argumentsJson) ?? new StoreArgs();

                if (string.IsNullOrWhiteSpace(args.Title))
                {
                    return InvokeResult<string>.FromError("session_memory_store requires 'title'.");
                }

                if (string.IsNullOrWhiteSpace(args.Summary))
                {
                    return InvokeResult<string>.FromError("session_memory_store requires 'summary' (1-2 lines).");
                }

                var tags = (args.Tags ?? new List<string>()).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                var note = new AgentSessionMemoryNote
                {
                    Title = args.Title.Trim(),
                    Summary = args.Summary.Trim(),
                    Details = string.IsNullOrWhiteSpace(args.Details) ? null : args.Details,
                    Tags = tags,
                    ConversationId = context.Request.ConversationId,
                    TurnSourceId = context.CurrentTurnId,
                    CreationDate = DateTime.UtcNow.ToString("o"),
                    CreatedByUser = context?.User
                };

                note.Kind = EntityHeader<AgentSessionMemoryNoteKinds>.Create(ParseKind(args.Kind));
                note.Importance = EntityHeader<AgentSessionMemoryNoteImportance>.Create(ParseImportance(args.Importance));

                var addResult = await _agentSessionManager.AddSessionMemoryNoteAsync(context.SessionId, note, context.Org, context.User);
                if (!addResult.Successful)
                {
                    return InvokeResult<string>.FromInvokeResult(addResult.ToInvokeResult());
                }

                var stored = addResult.Result;

                var payload = new StoreResult
                {
                    MemoryId = stored?.MemoryId,
                    Title = stored?.Title,
                    Summary = stored?.Summary,
                    Kind = stored?.Kind?.Value.ToString(),
                    Importance = stored?.Importance?.Value.ToString(),
                    Tags = stored?.Tags ?? new List<string>(),
                    ConversationId = context.Request?.ConversationId,
                    SessionId = context.SessionId,
                    TurnSourceId = stored.TurnSourceId,
                    CreationDate = stored.CreationDate
                };

                return InvokeResult<string>.Create(JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                _logger.AddException("[SessionMemoryStoreTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("session_memory_store failed to process arguments.");
            }
        }

        private static AgentSessionMemoryNoteImportance ParseImportance(string importance)
        {
            if (string.IsNullOrWhiteSpace(importance)) return AgentSessionMemoryNoteImportance.Normal;

            switch (importance.Trim().ToLowerInvariant())
            {
                case "low":
                    return AgentSessionMemoryNoteImportance.Low;
                case "normal":
                    return AgentSessionMemoryNoteImportance.Normal;
                case "high":
                    return AgentSessionMemoryNoteImportance.High;
                case "critical":
                    return AgentSessionMemoryNoteImportance.Critical;
                default:
                    return AgentSessionMemoryNoteImportance.Normal;
            }
        }

        private static AgentSessionMemoryNoteKinds ParseKind(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind)) return AgentSessionMemoryNoteKinds.Decision;

            switch (kind.Trim().ToLowerInvariant())
            {
                case "invariant":
                    return AgentSessionMemoryNoteKinds.Invariant;
                case "decision":
                    return AgentSessionMemoryNoteKinds.Decision;
                case "constraint":
                    return AgentSessionMemoryNoteKinds.Constraint;
                case "fact":
                    return AgentSessionMemoryNoteKinds.Fact;
                case "todo":
                    return AgentSessionMemoryNoteKinds.Todo;
                case "gotcha":
                    return AgentSessionMemoryNoteKinds.Gotcha;
                default:
                    return AgentSessionMemoryNoteKinds.Decision;
            }
        }

        public static object GetSchema()
        {
            return new
            {
                type = "function",
                name = ToolName,
                description = "Store a durable memory note on the current agent session (invariant/decision/constraint/etc.).",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        title = new { type = "string", description = "Short title for the memory note." },
                        summary = new { type = "string", description = "1-2 line summary (the marker)." },
                        details = new { type = "string", description = "Optional longer details; may include snippets." },
                        tags = new { type = "array", items = new { type = "string" }, description = "Optional tags (e.g., safety, parser, invariant)." },
                        importance = new { type = "string", description = "Importance: low|normal|high|critical (default normal)." },
                        kind = new { type = "string", description = "Kind: invariant|decision|constraint|fact|todo|gotcha (default decision)." }
                    },
                    required = new[] { "title", "summary" }
                }
            };
        }
    }
}
