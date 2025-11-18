using System.Collections.Generic;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Models
{
    public class ActiveFileDescriptor
    {
        public string Path { get; set; }
        public string Content { get; set; }
        public string ContentType { get; set; }
        public long SizeBytes { get; set; }
    }

    public class AgentExecutionRequest : IValidateable
    {
        public string SessionId { get; set; }
        public string PreviousTurnId { get; set; }
        public EntityHeader<OperationKinds> OperationKind { get; set; }

        public EntityHeader AgentContext { get; set; }
        public EntityHeader ConversationContext { get; set; }
        public string WorkspaceId { get; set; }
        public string Repo { get; set; }
        public string Language { get; set; }

        public string Instruction { get; set; }

        public List<ActiveFileDescriptor> ActiveFiles { get; set; } = new List<ActiveFileDescriptor>();
        public Dictionary<string, string> RagFilters { get; set; } = new Dictionary<string, string>();
    }
}
