using LagoVista.Core.Models;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    internal interface IAgentKnowledgeProvider
    {
        List<EntityHeader> InstructionDdrs { get;  }

        List<EntityHeader> ReferenceDdrs { get; }
        List<EntityHeader> ToolBoxes { get; set; }

        List<EntityHeader> ActiveTools { get; }

        List<EntityHeader> AvailableTools { get; }

        List<string> Instructions { get; }
    
    }
}
