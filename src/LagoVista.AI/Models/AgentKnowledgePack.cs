using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    /// <summary>
    /// Agent Knowledge Pack (AKP) - a materialized, ready-to-render snapshot of
    /// knowledge assembled from AgentContext + AgentContextRole + ModeKey.
    ///
    /// Note: AKPs are not session-locked; they may change between turns.
    /// </summary>
    public sealed class AgentKnowledgePack
    {
        // Identity
        public string AgentContextId { get; set; }
        public string RoleId { get; set; }
        public string ModeKey { get; set; }

        // Welcome messages (plain text, optional)
        public string AgentWelcomeMessage { get; set; }
        public string ConversationWelcomeMessage { get; set; }
        public string ModeWelcomeMessage { get; set; }


        // Kind catalog used by PKP to render blocks generically.
        public Dictionary<KnowledgeKind, KnowledgeKindDescriptor> KindCatalog { get; set; } = new Dictionary<KnowledgeKind, KnowledgeKindDescriptor>();

      
        /// <summary>
        /// Active tool names are tools that are ready to use in the current request
        /// their usage data and schema are included in the request.
        /// </summary>
        public List<string> ActiveTools { get; set; } = new List<string>();

    }
}
