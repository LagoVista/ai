using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentToolRegistry
    {
        /// <summary>
        /// Try to resolve a server-side tool by name (or id).
        /// </summary>
        InvokeResult<IAgentTool> GetTool(string toolName);
        void RegisterTool<T>(string toolName) where T : IAgentTool;
        bool HasTool(string toolName);
    }
}
