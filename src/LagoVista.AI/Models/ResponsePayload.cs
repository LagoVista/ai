using LagoVista.Core.AI.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public class ResponsePayload
    {
        public string PrimaryOutputText { get; set; }
        public LlmUsage Usage { get; set; }
    }
}
