using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;

namespace LagoVista.AI.Models
{

    public class AgentToolExecutionContext
    {
        public AgentContext AgentContext { get; set; }
        public ConversationContext ConversationContext { get; set; }
        public AgentExecuteRequest Request { get; set; }
        public string SessionId { get; set; }
        public string CurrentTurnId { get; set; }
        public EntityHeader Org { get; set; }
        public EntityHeader User { get; set; }
    }
}
