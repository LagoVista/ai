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
    /// Recalls (pages in) one or more session memory notes so the LLM can bring them back into focus.
    /// </summary>
    public sealed class SessionMemoryRecallTool : IAgentTool
    {
        private readonly IAdminLogger _logger;
        private readonly IAgentSessionManager _agentSessionManager;
        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = "Recall (play back) one or more session memory notes by id or tag so the LLM can use them as binding context.";
        public const string ToolName = "session_memory_recall";
        public const string ToolSummary = "recall a memory note";
        public SessionMemoryRecallTool(IAdminLogger logger, IAgentSessionManager agentSessionManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _agentSessionManager = agentSessionManager ?? throw new ArgumentNullException(nameof(agentSessionManager));
        }

        private sealed class RecallArgs
        {
            public List<string> MemoryIds { get; set; }
            public string Tag { get; set; }
            public string Kind { get; set; }
            public bool? IncludeDetails { get; set; }
        }

        private sealed class RecallItem
        {
            public string MemoryId { get; set; }
            public string Title { get; set; }
            public string Summary { get; set; }
            public string Details { get; set; }
            public string Kind { get; set; }
            public string Importance { get; set; }
            public List<string> Tags { get; set; }
            public string CreationDate { get; set; }
        }

        private sealed class RecallResult
        {
            public List<RecallItem> Items { get; set; } = new List<RecallItem>();
            public string SessionId { get; set; }
            public int Count { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (context == null)
            {
                return InvokeResult<string>.FromError("session_memory_recall requires a valid execution context.");
            }

            if (string.IsNullOrWhiteSpace(context.SessionId))
            {
                return InvokeResult<string>.FromError("session_memory_recall requires a sessionId in the execution context.");
            }

            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return InvokeResult<string>.FromError("session_memory_recall requires a non-empty arguments object.");
            }

            try
            {
                var args = JsonConvert.DeserializeObject<RecallArgs>(argumentsJson) ?? new RecallArgs();
                var hasIds = args.MemoryIds != null && args.MemoryIds.Any(id => !string.IsNullOrWhiteSpace(id));
                var hasTag = !string.IsNullOrWhiteSpace(args.Tag);
                var hasKind = !string.IsNullOrWhiteSpace(args.Kind);
                if (!hasIds && !hasTag && !hasKind)
                {
                    return InvokeResult<string>.FromError("session_memory_recall requires at least one of memoryIds, tag, or kind.");
                }

                var includeDetails = !args.IncludeDetails.HasValue || args.IncludeDetails.Value;
                var ids = hasIds ? args.MemoryIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() : new List<string>();
                var recall = await _agentSessionManager.RecallSessionMemoryNotesAsync(context.SessionId, ids, string.IsNullOrWhiteSpace(args.Tag) ? null : args.Tag.Trim(), string.IsNullOrWhiteSpace(args.Kind) ? null : args.Kind.Trim(), includeDetails, context.Org, context.User);
                if (!recall.Successful)
                {
                    return InvokeResult<string>.FromInvokeResult(recall.ToInvokeResult());
                }

                var notes = recall.Result ?? new List<AgentSessionMemoryNote>();
                var items = notes.Select(n => new RecallItem { MemoryId = n.MemoryId, Title = n.Title, Summary = n.Summary, Details = includeDetails ? n.Details : null, Kind = n.Kind?.Value.ToString(), Importance = n.Importance?.Value.ToString(), Tags = n.Tags ?? new List<string>(), CreationDate = n.CreationDate }).ToList();
                var payload = new RecallResult
                {
                    Items = items,
                    SessionId = context?.Request?.SessionId,
                    Count = items.Count
                };
                return InvokeResult<string>.Create(JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                _logger.AddException("[SessionMemoryRecallTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("session_memory_recall failed to process arguments.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Recall (play back) session memory notes by id/tag/kind.", p =>
            {
                p.Any("memoryIds", "array", "Optional list of memory ids to recall (e.g., MEM-0042).");
                p.String("tag", "Optional tag filter.");
                p.String("kind", "Optional kind filter: invariant|decision|constraint|fact|todo|gotcha.");
                p.Boolean("includeDetails", "Whether to include details (default true).", required: true);
            });
        }
    }
}