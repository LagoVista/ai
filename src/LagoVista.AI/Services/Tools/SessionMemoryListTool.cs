using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Lists memory notes stored on the current agent session.
    /// </summary>
    public sealed class SessionMemoryListTool : IAgentTool
    {
        private readonly IAdminLogger _logger;
        private readonly IMemoryNoteRepo _memoryNoteRepo;
        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = "List durable session memory notes (IDs, titles, summaries). Use when the user asks what was saved or before wrap-up.";
        public const string ToolName = "session_memory_list";
        public const string ToolSummary = "list all memory notes in the system";
        public SessionMemoryListTool(IAdminLogger logger, IMemoryNoteRepo memoryNoteRepo)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryNoteRepo = memoryNoteRepo ?? throw new ArgumentNullException(nameof(memoryNoteRepo));
        }

        private sealed class ListArgs
        {
            public string Tag { get; set; }
            public string Kind { get; set; }
            public string ImportanceMin { get; set; }
            public int? Limit { get; set; }
        }

        private sealed class ListItem
        {
            public string MemoryId { get; set; }
            public string Title { get; set; }
            public string Summary { get; set; }
            public string Kind { get; set; }
            public string Importance { get; set; }
            public List<string> Tags { get; set; }
            public string CreationDate { get; set; }
        }

        private sealed class ListResult
        {
            public List<ListItem> Items { get; set; } = new List<ListItem>();
            public string SessionId { get; set; }
            public int Count { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            throw new ArgumentNullException();
        }



        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (context == null)
            {
                InvokeResult<string>.FromError("session_memory_list requires a valid execution context.");
            }

            try
            {
                var args = string.IsNullOrWhiteSpace(argumentsJson) ? new ListArgs() : (JsonConvert.DeserializeObject<ListArgs>(argumentsJson) ?? new ListArgs());
                var limit = args.Limit.HasValue && args.Limit.Value > 0 ? args.Limit.Value : 50;
                var result = await _memoryNoteRepo.GetMemoryNotesForSessionAsync(context.Envelope.Org.Id, context.Envelope.SessionId);
                var list = result.Model;

                var items = (list ?? new List<AgentSessionMemoryNote>()).Select(n => new ListItem { MemoryId = n.MemoryId, Title = n.Title, Summary = n.Summary, Kind = n.Kind?.Value.ToString(), Importance = n.Importance?.Value.ToString(), Tags = n.Tags ?? new List<string>(), CreationDate = n.CreationDate }).ToList();
                var payload = new ListResult
                {
                    Items = items,
                    SessionId = context.Session.Id,
                    Count = items.Count
                };
                return InvokeResult<string>.Create(JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                _logger.AddException("[SessionMemoryListTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError($"session_memory_list failed to process arguments {ex.Message}.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "List memory notes stored on the current agent session (IDs, titles, summaries).", p =>
            {
                p.String("tag", "Optional tag filter.");
                p.String("kind", "Optional kind filter: invariant|decision|constraint|fact|todo|gotcha.");
                p.String("importanceMin", "Optional minimum importance: low|normal|high|critical.");
                p.Integer("limit", "Optional max items to return (default 50).");
            });
        }
    }
}