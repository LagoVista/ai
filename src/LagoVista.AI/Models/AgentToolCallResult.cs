using PdfSharpCore.Pdf.Content;
using System;
using System.Collections.Generic;
using System.Security.Policy;
using System.Text;

namespace LagoVista.AI.Models
{
    public class AgentToolCallResult
    {
        public string ToolCallId { get; set; }
        public bool RequiresClientExecution { get; set; }
        public string Name { get; set; }
        public int ExecutionMs { get; set; }
        public string ResultJson { get; set; }
        public string ErrorMessage { get; set; }
    }
}
