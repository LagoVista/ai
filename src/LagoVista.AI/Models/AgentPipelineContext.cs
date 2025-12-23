using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace LagoVista.AI.Models
{
    public sealed class AgentPipelineContext
    {
        public AgentPipelineContext()
        {
            CancellationToken = default;
        }


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

        public string SessionId => Session?.Id;

        // Derived / working values
        public string RagContextBlock { get; set; } = string.Empty;

        // Output (set by final step)
        public AgentExecuteResponse Response { get; set; }

        // Trace (optional)
        public CompositionTrace Trace { get; set; } = new CompositionTrace();

        public CancellationToken CancellationToken { get; set; } 


        public AgentToolExecutionContext ToToolContext()
        {
            return new AgentToolExecutionContext()
            {
                AgentContext = AgentContext,
                ConversationContext = ConversationContext,
                Org = Org,
                User = User,
                SessionId = Session.Id,
                CurrentTurnId = Turn.Id,
                Request = Request,
            
            };
        }
    }
}