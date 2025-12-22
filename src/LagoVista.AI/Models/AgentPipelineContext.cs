using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public sealed class AgentPipelineContext
    {
        // Identity / correlation
        public string CorrelationId { get; set; }
        public EntityHeader Org { get; set; }
        public EntityHeader User { get; set; }

        public AgentSession Session { get; set; }
        public AgentSessionTurn Turn { get; set; }


        // Core request inputs
        public AgentExecuteRequest Request { get; set; }

        // Loaded context objects
        public AgentContext AgentContext { get; set; }
        public ConversationContext ConversationContext { get; set; }

        // Derived / working values
        public string ConversationId { get; set; }
        public string RagContextBlock { get; set; } = string.Empty;

        // Output (set by final step)
        public AgentExecuteResponse Response { get; set; }

        // Trace (optional)
        public CompositionTrace Trace { get; set; } = new CompositionTrace();
    }
}