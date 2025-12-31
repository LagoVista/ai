using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Qdrant;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Utils.Types.Nuviot.RagIndexing;
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
        private readonly IMemoryNoteRepo _memoryNoteRepo;
        private readonly IQdrantClient _qdrantClient;
        private readonly IEmbedder _embedder;
        private readonly ISerialNumberManager _serialNumberManager;

        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = "Store a durable session memory note only when the user explicitly asks to remember/save/write something down. Returns a short MemoryId and a one-line summary of what was stored.";
        public const string ToolName = "session_memory_store";
        public const string ToolSummary = "save a session memory note";
        public SessionMemoryStoreTool(IEmbedder embeder, IQdrantClient qdrantClient, IAdminLogger logger, IMemoryNoteRepo memoryNoteRepo, ISerialNumberManager serialNumberManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryNoteRepo = memoryNoteRepo ?? throw new ArgumentNullException(nameof(memoryNoteRepo));
            _embedder = embeder ?? throw new ArgumentNullException(nameof(embeder));
            _qdrantClient = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
            _serialNumberManager = serialNumberManager ?? throw new ArgumentNullException(nameof(serialNumberManager));
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
            public string SessionId { get; set; }
            public string TurnSourceId { get; set; }
            public string CreationDate { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }


        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return InvokeResult<string>.FromError("session_memory_store requires a non-empty arguments object.");
            }

            if (context == null)
            {
                return InvokeResult<string>.FromError("session_memory_store requires a valid execution context.");
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

                var sn = await _serialNumberManager.GenerateSerialNumber(context.Envelope.Org.Id, "MEMNOTE","NA", 100);

                var tags = (args.Tags ?? new List<string>()).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var note = new AgentSessionMemoryNote
                {
                    Id = Guid.NewGuid().ToId(),
                    Title = args.Title.Trim(),
                    Summary = args.Summary.Trim(),
                    Details = string.IsNullOrWhiteSpace(args.Details) ? null : args.Details,
                    Tags = tags,
                    MemoryId = $"MEM-{sn:00000000}",
                    SessionId = context.Session.Id,
                    TurnSourceId = context.ThisTurn.Id,
                    CreationDate = context.TimeStamp,
                    CreatedByUser = context.Envelope.User
                };
                note.Kind = EntityHeader<AgentSessionMemoryNoteKinds>.Create(ParseKind(args.Kind));
                note.Importance = EntityHeader<AgentSessionMemoryNoteImportance>.Create(ParseImportance(args.Importance));

                await _memoryNoteRepo.AddMemoryNoteAsync(note);

                var payload = new StoreResult
                {
                    MemoryId = note.MemoryId,
                    Title = note.Title,
                    Summary = note.Summary,
                    Kind = note.Kind.Text,
                    Importance = note.Importance.Text,
                    Tags = note.Tags ?? new List<string>(),
                    SessionId = context.Session.Id,
                    TurnSourceId = context.ThisTurn.Id,
                    CreationDate = context.TimeStamp
                };

                var vector = await _embedder.EmbedAsync(note.Summary);

                var point = new RagPoint()
                {
                    PointId = Guid.NewGuid().ToString(),
                    Vector = vector.Result.Vector,
                    Payload = new RagVectorPayload()
                    {
                        DocId = note.Id,
                        OrgNamespace = context.Envelope.Org.Id,
                        Repo = "n/a",
                        RepoBranch = "n/a",
                        CommitSha = "n/a",
                        Title = note.Title,
                        SectionKey = "MemoryNote",
                        EmbeddingModel = vector.Result.EmbeddingModel,
                        BusinessDomainKey = "General",
                        ContentTypeId = RagContentType.Spec,
                        Subtype = "memory_note",
                        SubtypeFlavor = "Default",
                        Language = "en-US",
                    }
                };

                await _qdrantClient.UpsertAsync(context.AgentContext.VectorDatabaseCollectionName, new[] { point }, context.CancellationToken);
                return InvokeResult<string>.Create(JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                _logger.AddException("[SessionMemoryStoreTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError($"session_memory_store failed to process arguments - {ex.Message}");
            }
        }

        public static AgentSessionMemoryNoteImportance ParseImportance(string importance)
        {
            if (string.IsNullOrWhiteSpace(importance))
                return AgentSessionMemoryNoteImportance.Normal;
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

        public static AgentSessionMemoryNoteKinds ParseKind(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
                return AgentSessionMemoryNoteKinds.Decision;
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

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Store a durable memory note on the current agent session (invariant/decision/constraint/etc.).", p =>
            {
                p.String("title", "Short title for the memory note.", required: true);
                p.String("summary", "1-2 line summary (the marker).", required: true);
                p.String("details", "Optional longer details; may include snippets.", required: true);
                p.StringArray("tags", "Optional tags (e.g., safety, parser, invariant).");
                p.String("importance", "Importance: low|normal|high|critical (default normal).");
                p.String("kind", "Kind: invariant|decision|constraint|fact|todo|gotcha (default decision).");
            });
        }
    }
}