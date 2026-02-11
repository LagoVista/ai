using LagoVista.AI.Models.Resources;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LagoVista.AI.Models
{
    public enum AgentSessionTurnStatuses
    {
        [EnumLabel(AgentSessionTurn.AgentSessionTurnStatuses_New, AIResources.Names.Common_Status_New, typeof(AIResources))]
        New,

        [EnumLabel(AgentSessionTurn.AgentSessionTurnStatuses_Pending, AIResources.Names.Common_Status_Pending, typeof(AIResources))]
        Pending,

        [EnumLabel(AgentSessionTurn.AgentSessionTurnStatuses_Completed, AIResources.Names.Common_Status_Completed, typeof(AIResources))]
        Completed,

        [EnumLabel(AgentSessionTurn.AgentSessionTurnStatuses_Aborted, AIResources.Names.Common_Status_Aborted, typeof(AIResources))]
        Aborted,

        [EnumLabel(AgentSessionTurn.AgentSessionTurnStatuses_Failed, AIResources.Names.Common_Status_Failed, typeof(AIResources))]
        Failed,

        [EnumLabel(AgentSessionTurn.AgentSessionTurnStatuses_RolledBackTurn, AIResources.Names.AgentSessionTurnStatuses_RolledBackTurn, typeof(AIResources))]
        RolledBackTurn,
    }

    public enum OperationKinds
    {
        [EnumLabel(AgentSession.OperationKind_Code, AIResources.Names.Common_Status_Failed, typeof(AIResources))]
        Code,
        [EnumLabel(AgentSession.OperationKind_Image, AIResources.Names.Common_Status_Failed, typeof(AIResources))]
        Image,
        [EnumLabel(AgentSession.OperationKind_Text, AIResources.Names.Common_Status_Failed, typeof(AIResources))]
        Text,
        [EnumLabel(AgentSession.OperationKind_Domain, AIResources.Names.Common_Status_Failed, typeof(AIResources))]
        Domain
    }

    public enum AgentSessionMemoryNoteImportance
    {
        Low,
        Normal,
        High,
        Critical
    }

    public enum AgentSessionMemoryNoteKinds
    {
        Invariant,
        Decision,
        Constraint,
        Fact,
        Todo,
        Gotcha
    }

    public enum AgentSessionTurnType
    {
        [EnumLabel(AgentSessionTurn.AgentSessionTurnTypes_Initial, AIResources.Names.AgentSessionTurnTypes_Initial, typeof(AIResources))]
        Initial,
        [EnumLabel(AgentSessionTurn.AgentSessionTurnTypes_ChapterStart, AIResources.Names.AgentSessionTurnTypes_ChapterStart, typeof(AIResources))]
        ChapterStart,
        [EnumLabel(AgentSessionTurn.AgentSessionTurnTypes_Normal, AIResources.Names.AgentSessionTurnTypes_Normal, typeof(AIResources))]
        Normal,
        [EnumLabel(AgentSessionTurn.AgentSessionTurnTypes_ChapterEnd, AIResources.Names.AgentSessionTurnTypes_ChapterEnd, typeof(AIResources))]
        ChapterEnd
    }


    /// <summary>
    /// Tiny operational capsule stored as JSON on AgentSession (CurrentCapsuleJson).
    /// Stored as JSON to avoid schema churn.
    /// </summary>
    public class ContextCapsule
    {

        public int ChapterIndex { get; set; }

        public string ChapterTitle { get; set; }

        public string PreviousChapterSummary { get; set; }
    }

    [EntityDescription(
        AIDomain.AIAdmin, AIResources.Names.AgentSession_Title, AIResources.Names.AgentSession_Help, AIResources.Names.AgentSession_Description,
        EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIDomain),

        GetListUrl: "/api/ai/agent/sessions", GetUrl: "/api/ai/agent/session/{id}",

        ListUIUrl: "/mlworkbench/aptixsessions", EditUIUrl: "/mlworkbench/aptixsession/{id}",

        Icon: "icon-ae-creativity", ClusterKey: "agent", ModelType: EntityDescriptionAttribute.ModelTypes.RuntimeArtifact,
        Shape: EntityDescriptionAttribute.EntityShapes.Entity, Lifecycle: EntityDescriptionAttribute.Lifecycles.RunTime,
        Sensitivity: EntityDescriptionAttribute.Sensitivities.Internal, IndexInclude: false, IndexTier: EntityDescriptionAttribute.IndexTiers.Exclude,
        IndexPriority: 10, IndexTagsCsv: "ai,agent,runtime")]
    public class AgentSession : EntityBase, ISummaryFactory, IValidateable
    {
        public const string OperationKind_Code = "code";
        public const string OperationKind_Image = "image";
        public const string OperationKind_Text = "text";
        public const string OperationKind_Domain = "domain";

        public const string DefaultBranch = "main";
        public const string DefaultMode = "general";

        public EntityHeader AgentContext { get; set; }

        [Obsolete]
        public EntityHeader ConversationContext { get; set; }

        private EntityHeader _role;
#pragma warning disable CS0612 // Type or member is obsolete
        public EntityHeader Role { get => _role ?? ConversationContext; set => _role = value; }
#pragma warning restore CS0612 // Type or member is obsolete

        public Dictionary<string, string> DdrCache { get; set; } = new Dictionary<string, string>();

        public string WorkspaceId { get; set; }

        public string Repo { get; set; }

        /// <summary>
        /// Eventually this will go away...
        /// </summary>
        public string Mode { get; set; } = DefaultMode;

        public EntityHeader AgentMode { get; set; }

        public string CurrentBranch { get; set; } = DefaultBranch;

        public string ModeSetTimestamp { get; set; }

        public string ModeReason { get; set; }

        public EntityHeader AgentPersona { get; set; }

        public string DefaultLanguage { get; set; }

        public EntityHeader CurrentChapter { get; set; } 

        /// <summary>
        /// Current chapter index for the session.
        /// </summary>
        public int CurrentChapterIndex { get; set; } = 0;

        /// <summary>
        /// When we create a new chapter, we generate a seed value to help guide LLM responses
        /// based on progress in the previous channel
        /// </summary>
        public string ChapterSeed { get; set; }

        /// <summary>
        /// Archive pointers for completed chapters.
        /// </summary>
        public List<AgentSessionChapter> Chapters { get; set; } = new List<AgentSessionChapter>();

        /// <summary>
        /// JSON serialized ContextCapsule.
        /// </summary>
        public ContextCapsule CurrentCapsule { get; set; }

        public EntityHeader<OperationKinds> OperationKind { get; set; }

        public List<AgentSessionTurn> Turns { get; set; } = new List<AgentSessionTurn>();

        public List<AgentSessionCheckpoint> Checkpoints { get; set; } = new List<AgentSessionCheckpoint>();

        public List<ModeHistory> ModeHistory { get; set; } = new List<ModeHistory>();

        public List<TouchedFile> TouchedFiles { get; set; } = new List<TouchedFile>();    
        public List<AgentSessionListDefinition> Lists { get; set; } = new List<AgentSessionListDefinition>();   

        /// <summary>
        /// List of KFRs (Short Term Memory for Session)
        /// </summary>
        public Dictionary<string, List<AgentSessionKfrEntry>> Kfrs { get; set; } = new Dictionary<string, List<AgentSessionKfrEntry>>();

        /// <summary>
        /// Durable lineage of restore / branch operations performed against this session.
        /// Stored on the branched session so the user can view restore history later.
        /// </summary>
        public List<AgentSessionRestoreReport> RestoreReports { get; set; } = new List<AgentSessionRestoreReport>();

        /// <summary>
        /// Minimal lineage fields for quick navigation. These are also reflected in RestoreReports.
        /// </summary>
        public string SourceSessionId { get; set; }

        public string SourceCheckpointId { get; set; }

        public string SourceTurnSourceId { get; set; }

        public string RestoreOperationId { get; set; }

        public string RestoredOnUtc { get; set; }

        public bool Completed { get; set; }

        public bool Shared { get; set; }

        public bool Archived { get; set; }
        public int TotalTokenCount { get; set; }

        public AgentSessionSummary CreateSummary()
        {
            var summary = new AgentSessionSummary();
            summary.Populate(this);
            summary.DiscussionsTotal = Turns.Count;
            summary.AgentContextId = AgentContext?.Id;
            summary.AgentContextName = AgentContext?.Text;
            summary.RoleContextName = Role?.Text;
            summary.RoleContextId = Role?.Id;
            summary.TurnCount = Turns.Count;
            summary.Mode = Mode;
            summary.ModeSetTimestamp = ModeSetTimestamp;
            summary.ModeReason = ModeReason;
            summary.Shared = Shared;
            summary.Completed = Completed;
            summary.Archived = Archived;
            summary.Chapters = Chapters.Select(arc=> EntityHeader.Create(arc.Id, arc.Title)).ToList();

            var lastTurn = Turns.LastOrDefault();
            if (lastTurn != null)
            {
                summary.LastTurnDate = lastTurn.CreationDate;
                summary.LastTurnStatus = lastTurn.Status?.Text;
            }

            return summary;
        }

        ISummaryData ISummaryFactory.CreateSummary()
        {
            return CreateSummary();
        }
    }

    public class AgentSessionMemoryNote
    {
        public string Id { get; set; } = Guid.NewGuid().ToId();

        /// <summary>
        /// Stable, short identifier displayed to the user and referenced later (e.g., MEM-0042).
        /// </summary>
        public string MemoryId { get; set; }

        public string Title { get; set; }

        public string OrgId { get; set; }

        /// <summary>
        /// 1-2 line summary that acts as the "marker" in the conversation.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Optional longer detail; can include snippets.
        /// </summary>
        public string Details { get; set; }

        public EntityHeader<AgentSessionMemoryNoteImportance> Importance { get; set; } =
            EntityHeader<AgentSessionMemoryNoteImportance>.Create(AgentSessionMemoryNoteImportance.Normal);

        public EntityHeader<AgentSessionMemoryNoteKinds> Kind { get; set; } =
            EntityHeader<AgentSessionMemoryNoteKinds>.Create(AgentSessionMemoryNoteKinds.Decision);

        public List<string> Tags { get; set; } = new List<string>();

        public string CreationDate { get; set; }

        public EntityHeader CreatedByUser { get; set; }

        public string TurnSourceId { get; set; }

        public string SessionId { get; set; }
    }

    public class TouchedFile
    {
        public string Path { get; set; }

        public string ContentHash { get; set; }

        public string LastAccess {get; set;}
    } 

    public class ModeHistory
    {
        public string PreviousMode { get; set; }
        public string NewMode { get; set; }
        public string TimeStamp { get; set; }
        public string Reason { get; set; }
    }

    public class AgentSessionTurn : IValidateable
    {
        public const string AgentSessionTurnStatuses_New = "new";
        public const string AgentSessionTurnStatuses_Pending = "pending";
        public const string AgentSessionTurnStatuses_Completed = "completed";
        public const string AgentSessionTurnStatuses_Failed = "failed";
        public const string AgentSessionTurnStatuses_Aborted = "aborted";
        public const string AgentSessionTurnStatuses_RolledBackTurn = "rolledbackturn";
        public const string AgentSessionTurnStatuses_ChapterEnd = "chapaterEnd";

        public const string AgentSessionTurnTypes_Initial = "initial";
        public const string AgentSessionTurnTypes_ChapterStart = "chapaterStart";
        public const string AgentSessionTurnTypes_Normal = "normal";
        public const string AgentSessionTurnTypes_ChapterEnd = "chapaterEnd";


        public string Id { get; set; } = Guid.NewGuid().ToId();

        public int SequenceNumber { get; set; }

        public EntityHeader CreatedByUser { get; set; }

        /// <summary>
        /// Timestamp when this turn was created.
        /// </summary>
        public string CreationDate { get; set; }

        /// <summary>
        /// Timestamp when the OpenAI response was received, if any.
        /// </summary>
        public string OpenAIResponseReceivedDate { get; set; }

        /// <summary>
        /// Timestamp when this response chain expires for previous_response_id reuse.
        /// </summary>
        public string OpenAIChainExpiresDate { get; set; }

        /// <summary>
        /// Timestamp of the last status change for this turn.
        /// </summary>
        public string StatusTimeStamp { get; set; }

        public string Mode { get; set; }

        public string InstructionSummary { get; set; }
        public string OriginalInstructions { get; set; }
        public bool InstructionsTruncated { get; set; }

        public string AgentAnswerSummary { get; set; }

        public bool AgentAnswerTruncated { get; set; }

        public string SessionId { get; set; }

        public string OpenAIResponseId { get; set; }

        public string PreviousOpenAIResponseId { get; set; }

        public string OpenAIModel { get; set; }

        public string OpenAIRequestBlobUrl { get; set; }

        public string OpenAIResponseBlobUrl { get; set; }

        public double ExecutionMs { get; set; }

        public EntityHeader<AgentSessionTurnStatuses> Status { get; set; } =
            EntityHeader<AgentSessionTurnStatuses>.Create(AgentSessionTurnStatuses.New);

        public EntityHeader<AgentSessionTurnType> Type { get; set; } = EntityHeader<AgentSessionTurnType>.Create(AgentSessionTurnType.Normal);

        public List<AgentSessionChunkRef> ChunkRefs { get; set; } = new List<AgentSessionChunkRef>();

        public List<AgentSessionActiveFileRef> ActiveFileRefs { get; set; } = new List<AgentSessionActiveFileRef>();

        public List<string> Warnings { get; set; } = new List<string>();

        public List<string> Errors { get; set; } = new List<string>();

        public int PromptTokens { get; set; }

        public int CompletionTokens { get; set; }

        public int TotalTokens { get; set; }

        public int ReasoningTokens { get; set; }

        public int CachedTokens { get; set; }

        public List<AgentSessionTurnIteration> Iterations { get; set; } = new List<AgentSessionTurnIteration>();
    }

    public class AgentSessionTurnIteration
    {
        public string Id { get; set; } = Guid.NewGuid().ToId();

        public int Index { get; set; }

        public double ExecutionMs { get; set; }

        public EntityHeader<AgentSessionTurnStatuses> Status { get; set; } = EntityHeader<AgentSessionTurnStatuses>.Create(AgentSessionTurnStatuses.New);

        public string PreviousOpenAIResponseId { get; set; }

        public string OpenAiResponseId { get; set; }

        public int PromptTokens { get; set; }

        public int CompletionTokens { get; set; }

        public int TotalTokens { get; set; }

        public int ReasoningTokens { get; set; }

        public int CachedTokens { get; set; }

        public List<string> ToolCalls { get; set; }

        public List<string> Errors { get; set; } = new List<string>();

        public string OpenAIRequestBlobUrl { get; set; }

        public string OpenAIResponseBlobUrl { get; set; }

        /// <summary>
        /// Timestamp of the last status change for this turn.
        /// </summary>
        public string StatusTimeStamp { get; set; }
    }

    public class AgentSessionChunkRef
    {
        public string ChunkId { get; set; }

        public string Path { get; set; }

        public int StartLine { get; set; }

        public int EndLine { get; set; }

        public string ContentHash { get; set; }
    }

    public class AgentSessionActiveFileRef
    {
        public string Path { get; set; }

        public string ContentHash { get; set; }

        public long SizeBytes { get; set; }

        public bool IsTouched { get; set; }

        public bool WasSentToLLM { get; set; }

        public bool WasTooLargeToSend { get; set; }
    }

    public class AgentSessionCheckpoint
    {
        /// <summary>
        /// Internal persistence id.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToId();

        /// <summary>
        /// Short, stable, user-facing identifier (e.g. CP-0007).
        /// </summary>
        public string CheckpointId { get; set; }

        /// <summary>
        /// User-provided name for the checkpoint.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Optional notes describing why this checkpoint was created.
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// Turn source id captured at checkpoint time.
        /// Used as a fallback if TurnId is unavailable.
        /// </summary>
        public string TurnSourceId { get; set; }

        /// <summary>
        /// Conversation id at time of capture (diagnostics / traceability).
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// UTC timestamp when the checkpoint was created.
        /// </summary>
        public string CreationDate { get; set; }

        /// <summary>
        /// User who created the checkpoint.
        /// </summary>
        public EntityHeader CreatedByUser { get; set; }
    }

    public enum KfrKind
    {
        Goal,
        Plan,
        ActiveContract,
        Constraint,
        OpenQuestion
    }

    public sealed class AgentSessionKfrEntry
    {
        public string CreatedByUser { get; set; }

        public string CreatedByUserId { get; set; }

        public string KfrId { get; set; }

        public KfrKind Kind { get; set; }

        public string Value { get; set; }

        public bool RequiresResolution { get; set; }

        public bool IsActive { get; set; } = true;

        public List<string> Tags { get; set; } = new List<string>();
        public string Category { get; set; }

        public string CreationDate { get; set; } = DateTime.UtcNow.ToString("o");

        public string LastUpdatedDate { get; set; } = DateTime.UtcNow.ToString("o");
    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AgentSessions_Title, AIResources.Names.AgentSession_Help, AIResources.Names.AgentSession_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIDomain),
        Icon: "icon-ae-creativity", ListUIUrl: "/mlworkbench/aptixsessions", EditUIUrl: "/mlworkbench/aptixsession/{id}", GetListUrl: "/api/ai/agent/sessions", GetUrl: "/api/ai/agent/session/{id}")]
    public class AgentSessionSummary : SummaryData
    {
        public string AgentContextId { get; set; }

        public string RoleContextId { get; set; }

        public string AgentContextName { get; set; }

        public string RoleContextName { get; set; }

        public string LastTurnStatus { get; set; }

        public string LastTurnDate { get; set; }

        public int TurnCount { get; set; }

        public string Mode { get; set; }

        public string ModeSetTimestamp { get; set; }

        public string ModeReason { get; set; }

        public bool Shared { get; set; }

        public bool Completed { get; set; }

        public bool Archived { get; set; }
    
        public List<EntityHeader> Chapters { get; set; } 
    }

      /// <summary>
    /// Top-level list definition. Contains optional schema fields for item metadata.
    /// </summary>
    public class AgentSessionListDefinition
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Unique across all lists.
        /// </summary>
        public string Slug { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; }

        public int SchemaVersion { get; set; } = 1;

        public List<AgentSessionListItem> Items { get; set; } = new List<AgentSessionListItem>();


        public List<AgentSessionFieldDefinition> Fields { get; set; } = new List<AgentSessionFieldDefinition>();

        public string CreationDate { get; set; } = DateTime.UtcNow.ToString("o");

        public string LastUpdatedDate { get; set; } = DateTime.UtcNow.ToString("o");
    }

    /// <summary>
    /// Defines a single metadata field in a list schema.
    /// </summary>
    public class AgentSessionFieldDefinition
    {
        /// <summary>
        /// Unique within the list. Used as the key in ListItem.Data.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        [JsonConverter(typeof(StringEnumConverter))]
        public AgentSessionListFieldDataType Type { get; set; } = AgentSessionListFieldDataType.Text;

        public bool Required { get; set; }

        /// <summary>
        /// Only applicable when Type == Enum.
        public List<string> EnumValues { get; set; } = new List<string>();

        public int SortOrder { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum AgentSessionListFieldDataType
    {
        Text,
        Number,
        Bool,
        Date,
        DateTime,
        Enum
    }

    /// <summary>
    /// Item within a list. Optional Data dictionary holds values for schema-defined fields.
    /// </summary>
    public class AgentSessionListItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ListId { get; set; }

        /// <summary>
        /// Unique within the list.
        /// </summary>
        public string Slug { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; }

        /// <summary>
        /// Used for ordering/reordering. V1 can renumber freely.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Metadata values keyed by FieldDefinition.Key.
        /// Use JToken to support multiple primitive types while staying Json.NET-friendly.
        /// </summary>
        public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string CreationDate { get; set; } = DateTime.UtcNow.ToString("o");

        public string LastUpdatedDate { get; set; } = DateTime.UtcNow.ToString("o");
    }
}
