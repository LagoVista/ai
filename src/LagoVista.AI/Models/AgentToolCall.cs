using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public class AgentToolCall
    {
        public string ToolCallId { get; set; }
        public string Name { get; set; }
        public string ArgumentsJson { get; set; }

    }
}
