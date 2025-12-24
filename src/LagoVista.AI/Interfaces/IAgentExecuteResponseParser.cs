using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentExecuteResponseParser
    {
        InvokeResult<AgentExecuteResponse> Parse(string rawJson, AgentExecuteRequest request);  
    }
}
