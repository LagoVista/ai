using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public class AgentTurnChatHistory
    {
        public string TimeStamp { get; set; }
        public string TurnId { get; set; }
        public string Sessionid { get; set; }

        public string UserInstructions { get; set; }
        public string ModelResponseText { get; set; }
        public bool ModelResponseTextTruncated { get; set; }
        public bool UserInstructionsTruncated { get; set; }

    }
}
