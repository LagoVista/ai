using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    /// <summary>
    /// Agent Knowledge Pack (AKP) - a materialized, ready-to-render snapshot of
    /// knowledge assembled from AgentContext + ConversationContext + Mode.
    ///
    /// Note: AKPs are not session-locked; they may change between turns.
    /// </summary>
    public sealed class AgentKnowledgePack
    {
        // Identity
        public string AgentContextId { get; set; }
        public string ConversationContextId { get; set; }
        public string Mode { get; set; }

        // Welcome messages (plain text, optional)
        public string AgentWelcomeMessage { get; set; }
        public string ConversationWelcomeMessage { get; set; }
        public string ModeWelcomeMessage { get; set; }


        // Kind catalog used by PKP to render blocks generically.
        public Dictionary<KnowledgeKind, KnowledgeKindDescriptor> KindCatalog { get; set; }
            = new Dictionary<KnowledgeKind, KnowledgeKindDescriptor>();

        // Tools enabled for this pack (deduped). PKP uses this to attach tool schemas.
        public List<string> EnabledToolNames { get; set; } = new List<string>();
    }
}
