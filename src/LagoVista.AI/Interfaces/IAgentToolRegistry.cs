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
        void RegisterTool<T>() where T : IAgentTool;
        bool HasTool(string toolName);

        Type GetToolType(string toolName);
        /// <summary>
        /// Returns a read-only map of toolName -> concrete tool Type
        /// for all registered tools.
        /// </summary>
        IReadOnlyDictionary<string, Type> GetRegisteredTools();
    }
}
