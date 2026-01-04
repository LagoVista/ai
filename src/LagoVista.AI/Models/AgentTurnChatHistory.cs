using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public class AgentTurnChatHistory
    {

        public DateTimeOffset TsUtc { get; set; }
        public string TurnId { get; set; }

        public string UserInstructions { get; set; }
        public string ModelResponseText { get; set; }
    }
}
