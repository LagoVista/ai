using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models.Context
{
    public class ToolCallManifest
    {
        public List<AgentToolCall> ToolCalls { get; set; } = new List<AgentToolCall>();
        public List<AgentToolCallResult> ToolCallResults { get; set; } = new List<AgentToolCallResult>();
    }
}
