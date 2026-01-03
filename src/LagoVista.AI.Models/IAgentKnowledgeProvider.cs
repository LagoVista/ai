using LagoVista.Core.Models;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    public interface IAgentKnowledgeProvider
    {
        List<EntityHeader> InstructionDdrs { get;  }

        List<EntityHeader> ReferenceDdrs { get; }
      
        List<EntityHeader> ActiveTools { get; }

        List<EntityHeader> AvailableTools { get; }

        List<string> Instructions { get; }    
    }

    public interface IToolBoxProvider
    {
        List<EntityHeader> ToolBoxes { get; set; }
    }
}
