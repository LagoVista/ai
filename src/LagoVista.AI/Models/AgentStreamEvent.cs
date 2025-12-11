using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public class AgentStreamEvent
    {
        // "partial", "final", maybe "error"
        public string Kind { get; set; }

        // For partial text tokens, tool messages, etc.
        public string? DeltaText { get; set; }

        // Optionally any metadata (e.g., index, role)
        public int? Index { get; set; }

        // Only set on Kind == "final"
        public InvokeResult<AgentExecuteResponse>? Final { get; set; }
    }
}
