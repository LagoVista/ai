using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using SixLabors.Fonts.Tables.AdvancedTypographic;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Recalls (pages in) one or more session memory notes so the LLM can bring them back into focus.
    /// </summary>
    public sealed class SessionMemoryRecallTool : IAgentTool
    {
        private readonly IAdminLogger _logger;
        private readonly IMemoryNoteRepo _memoryNoteRepo;
        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = "Recall (play back) one or more session memory notes by id or tag so the LLM can use them as binding context.";
        public const string ToolName = "session_memory_recall";
        public const string ToolSummary = "recall a memory note";
        public SessionMemoryRecallTool(IAdminLogger logger, IMemoryNoteRepo memoryNoteRepo)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryNoteRepo = memoryNoteRepo ?? throw new ArgumentNullException(nameof(memoryNoteRepo));
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
            public int Count { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (context == null)
            {
                return InvokeResult<string>.FromError("session_memory_recall requires a valid execution context.");
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

                var notes = await _memoryNoteRepo.GetMemoryNotesForSessionAsync(context.Envelope.Org.Id, context.Session.Id);

                IEnumerable<AgentSessionMemoryNote> query = notes.Model;

                if (ids.Count > 0) query = query.Where(n => !string.IsNullOrWhiteSpace(n.MemoryId) && ids.Contains(n.MemoryId, StringComparer.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(args.Tag))
                {
                    var t = args.Tag.Trim();
                    query = query.Where(n => n.Tags != null && n.Tags.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)));
                }
                if (!String.IsNullOrEmpty(args.Kind))
                {
                    var kindFilter = SessionMemoryStoreTool.ParseKind(args.Kind);
                    query = query.Where(n => n.Kind != null && n.Kind.Value == kindFilter);
                }
                var results = query.OrderByDescending(n => n.CreationDate).ThenByDescending(n => n.MemoryId).ToList();

                if (!includeDetails)
                {
                    results = results.Select(n => new AgentSessionMemoryNote
                    {
                        Id = n.Id,
                        MemoryId = n.MemoryId,
                        Title = n.Title,
                        Summary = n.Summary,
                        Details = null,
                        Importance = n.Importance,
                        Kind = n.Kind,
                        Tags = n.Tags == null ? new List<string>() : new List<string>(n.Tags),
                        CreationDate = n.CreationDate,
                        CreatedByUser = n.CreatedByUser,
                        TurnSourceId = n.TurnSourceId,
                        SessionId = n.SessionId
                    }).ToList();
                }

                var items = results.Select(n => new RecallItem { MemoryId = n.MemoryId, Title = n.Title, Summary = n.Summary, Details = includeDetails ? n.Details : null, Kind = n.Kind?.Value.ToString(), Importance = n.Importance?.Value.ToString(), Tags = n.Tags ?? new List<string>(), CreationDate = n.CreationDate }).ToList();
                var payload = new RecallResult
                {
                    Items = items,
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
                p.StringArray("memoryIds", "Optional list of memory ids to recall (e.g., MEM-0042).");
                p.String("tag", "Optional tag filter.");
                p.String("kind", "Optional kind filter: invariant|decision|constraint|fact|todo|gotcha.");
                p.Boolean("includeDetails", "Whether to include details (default true).", required: true);
            });
        }
    }
}