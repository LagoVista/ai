using System.Collections.Generic;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Models
{
    public enum AgentClientKind
    {
        Browser,
        Cli,
        ThickClient,
        Other
    }

    /// <summary>
    /// Normalized inbound request from any client (browser, CLI, thick client).
    /// The AgentRequestHandler will map this into either a NewAgentExecutionSession
    /// or AgentExecutionRequest and dispatch to the orchestrator.
    /// </summary>
    public class AgentRequestEnvelope : IValidateable
    {
        /// <summary>
        /// Source client kind; can be used later to tailor responses for
        /// browser vs CLI vs thick client.
        /// </summary>
        public AgentClientKind ClientKind { get; set; } = AgentClientKind.Browser;

        /// <summary>
        /// If null or empty, this is treated as a new session; otherwise a
        /// follow-up turn for the specified session.
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Optional previous turn id within the session. If not provided for
        /// follow-up calls, the last turn in the session will be used.
        /// </summary>
        public string PreviousTurnId { get; set; }

        public EntityHeader<OperationKinds> OperationKind { get; set; }

        public EntityHeader AgentContext { get; set; }

        public EntityHeader Role { get; set; }

        public string WorkspaceId { get; set; }

        public string Repo { get; set; }

        public string Language { get; set; }

        /// <summary>
        /// Primary natural language instruction from the user.
        /// </summary>
        public string Instruction { get; set; }

        /// <summary>
        /// Logical list of active files provided by the client.
        /// </summary>
        public List<ActiveFileDescriptor> ActiveFiles { get; set; } = new List<ActiveFileDescriptor>();

        /// <summary>
        /// Optional RAG filters (e.g. repo, language, tags) supplied by the client.
        /// </summary>
        public Dictionary<string, string> RagFilters { get; set; } = new Dictionary<string, string>();
    }
}
