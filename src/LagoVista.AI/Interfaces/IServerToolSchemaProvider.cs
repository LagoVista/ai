using System.Collections.Generic;
using LagoVista.Core.AI.Models;

namespace LagoVista.AI.Interfaces
{
    public interface IServerToolSchemaProvider
    {

        /// <summary>
        /// Returns JSON-serializable tool definitions for the given set of
        /// server tool names. Tool names that are not registered are ignored.
        /// </summary>
        IReadOnlyList<object> GetToolSchemas(IEnumerable<string> toolNames);

        /// <summary>
        /// Returns a single JSON-serializable tool definition for the given
        /// server tool name, or null if the tool is not registered or the
        /// schema cannot be resolved.
        /// </summary>
        object GetToolSchema(string toolName);
    }
}
