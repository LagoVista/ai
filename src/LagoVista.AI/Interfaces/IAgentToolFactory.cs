using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentToolFactory
    {
        InvokeResult<IAgentTool> GetTool(string toolName);
    }
}
