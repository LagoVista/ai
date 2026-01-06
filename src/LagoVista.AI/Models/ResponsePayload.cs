using LagoVista.Core.AI.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public class ResponsePayload
    {
        public string PrimaryOutputText { get; set; }
        public List<FileRef> Files { get; set; } = new List<FileRef>();
        public LlmUsage Usage { get; set; } = new LlmUsage();
        public List<AcpIntent> AcpIntents { get; set; } = new List<AcpIntent>();
    }
}
