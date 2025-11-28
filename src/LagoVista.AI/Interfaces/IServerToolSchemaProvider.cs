using LagoVista.Core.AI.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Interfaces
{
    public interface IServerToolSchemaProvider
    {
        /// <summary>
        /// Returns JSON-serializable tool definitions (OpenAI-style) for
        /// all server tools applicable to this request/agent.
        /// </summary>
        IReadOnlyList<object> GetToolSchemas(AgentExecuteRequest request);
    }
}
