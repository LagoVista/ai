using LagoVista.AI.Models.Resources;
using LagoVista.Core;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
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

    public class AgentSession : EntityBase, ISummaryFactory, IValidateable
    {
        public const string OperationKind_Code = "code";
        public const string OperationKind_Image = "image";
        public const string OperationKind_Text = "text";
        public const string OperationKind_Domain = "domain";


        public EntityHeader AgentContext { get; set; }

        public EntityHeader ConversationContext { get; set; }

        public string WorkspaceId { get; set; }

        public string Repo { get; set; }

        public string DefaultLanguage { get; set; }

        public EntityHeader<OperationKinds> OperationKind { get; set; }

        public List<AgentSessionTurn> Turns { get; set; } = new List<AgentSessionTurn>();

        public AgentSessionSummary CreateSummary()
        {
            var summary = new AgentSessionSummary();
            summary.Populate(this);
            summary.DiscussionsTotal = Turns.Count;
            summary.AgentContextId = AgentContext.Id;
            summary.AgentContextName = AgentContext.Text;
            summary.ConversationContextName = ConversationContext.Text;
            summary.ConversationContextId = ConversationContext.Id;
            summary.TurnCount = Turns.Count;
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
            return this.CreateSummary();
        }
    }

    public class AgentSessionTurn : IValidateable
    {

        public const string AgentSessionTurnStatuses_New = "new";
        public const string AgentSessionTurnStatuses_Pending = "pending";
        public const string AgentSessionTurnStatuses_Completed = "completed";
        public const string AgentSessionTurnStatuses_Failed = "failed";
        public const string AgentSessionTurnStatuses_Aborted = "aborted";

        

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

        public string AgentAnswerSummary { get; set; }

        public string ConversationId { get; set; }

        public string OpenAIResponseId { get; set; }

        public string PreviousOpenAIResponseId { get; set; }

        public string OpenAIModel { get; set; }

        public string OpenAIRequestBlobUrl { get; set; }

        public string OpenAIResponseBlobUrl { get; set; }

        public double ExecutionMs { get; set; }

        public EntityHeader<AgentSessionTurnStatuses> Status { get; set; } =
            EntityHeader<AgentSessionTurnStatuses>.Create(AgentSessionTurnStatuses.New);

        public List<AgentSessionChunkRef> ChunkRefs { get; set; } = new List<AgentSessionChunkRef>();

        public List<AgentSessionActiveFileRef> ActiveFileRefs { get; set; } = new List<AgentSessionActiveFileRef>();

        public List<string> Warnings { get; set; } = new List<string>();

        public List<string> Errors { get; set; } = new List<string>();
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

    public class AgentSessionSummary : SummaryData
    {
        public string AgentContextId { get; set; }

        public string ConversationContextId { get; set; }

        public string AgentContextName { get; set; }

        public string ConversationContextName { get; set; }

        public string LastTurnStatus { get; set; }

        public string LastTurnDate { get; set; }

        public int TurnCount { get; set; }
    }
}
