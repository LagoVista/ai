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
    /// Lists memory notes stored on the current agent session.
    /// </summary>
    public sealed class SessionMemoryListTool : IAgentTool
    {
        private readonly IAdminLogger _logger;
        private readonly IAgentSessionManager _agentSessionManager;

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = "List durable session memory notes (IDs, titles, summaries). Use when the user asks what was saved or before wrap-up.";

        public const string ToolName = "session_memory_list";

        public SessionMemoryListTool(IAdminLogger logger, IAgentSessionManager agentSessionManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _agentSessionManager = agentSessionManager ?? throw new ArgumentNullException(nameof(agentSessionManager));
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
            public string ConversationId { get; set; }
            public string SessionId { get; set; }
            public int Count { get; set; }
        }
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (context == null)
            {
                return InvokeResult<string>.FromError("session_memory_list requires a valid execution context.");
            }

            if (string.IsNullOrWhiteSpace(context.SessionId))
            {
                return InvokeResult<string>.FromError("session_memory_list requires a sessionId in the execution context.");
            }

            try
            {
                var args = string.IsNullOrWhiteSpace(argumentsJson) ? new ListArgs() : (JsonConvert.DeserializeObject<ListArgs>(argumentsJson) ?? new ListArgs());

                var limit = args.Limit.HasValue && args.Limit.Value > 0 ? args.Limit.Value : 50;

                var list = await _agentSessionManager.ListSessionMemoryNotesAsync(
                    context.SessionId,
                    string.IsNullOrWhiteSpace(args.Tag) ? null : args.Tag.Trim(),
                    string.IsNullOrWhiteSpace(args.Kind) ? null : args.Kind.Trim(),
                    string.IsNullOrWhiteSpace(args.ImportanceMin) ? null : args.ImportanceMin.Trim(),
                    limit,
                    context.Org,
                    context.User);

                if (!list.Successful)
                {
                    return InvokeResult<string>.FromInvokeResult(list);
                }

                var items = (list.Model ?? new List<AgentSessionMemoryNote>()).Select(n => new ListItem
                {
                    MemoryId = n.MemoryId,
                    Title = n.Title,
                    Summary = n.Summary,
                    Kind = n.Kind?.Value.ToString(),
                    Importance = n.Importance?.Value.ToString(),
                    Tags = n.Tags ?? new List<string>(),
                    CreationDate = n.CreationDate
                }).ToList();

                var payload = new ListResult
                {
                    Items = items,
                    ConversationId = context?.Request?.ConversationId,
                    SessionId = context?.SessionId,
                    Count = items.Count
                };

                return InvokeResult<string>.Create(JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                _logger.AddException("[SessionMemoryListTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("session_memory_list failed to process arguments.");
            }
        }

        public static object GetSchema()
        {
            return new
            {
                type = "function",
                name = ToolName,
                description = "List memory notes stored on the current agent session (IDs, titles, summaries).",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        tag = new { type = "string", description = "Optional tag filter." },
                        kind = new { type = "string", description = "Optional kind filter: invariant|decision|constraint|fact|todo|gotcha." },
                        importanceMin = new { type = "string", description = "Optional minimum importance: low|normal|high|critical." },
                        limit = new { type = "integer", description = "Optional max items to return (default 50)." }
                    },
                    required = new string[] { }
                }
            };
        }
    }
}
