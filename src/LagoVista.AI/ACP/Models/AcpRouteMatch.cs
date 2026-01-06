using System;

namespace LagoVista.AI.ACP.Models
{
    public class AcpRouteMatch
    {
        public string CommandId { get; set; }
        public string DisplayName { get; set; }
        public AcpCommandPriority Priority { get; set; }

        public string MatchedTrigger { get; set; }
        public string[] Args { get; set; } = Array.Empty<string>();

        // Optional debug fields (router-owned, not passed to command unless you decide later)
        public string RemainderText { get; set; }
    }
}