using System.Collections.Generic;
using LagoVista.Core.AI.Models;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Provides LLM-facing usage guidance blocks for all registered Aptix server tools.
    ///
    /// The resulting string is intended to be injected into the system prompt
    /// alongside the OpenAI tool/function schemas.
    /// </summary>
    public interface IServerToolUsageMetadataProvider
    {
        /// <summary>
        /// Builds a delimited, human-readable block of usage guidance for all
        /// registered server tools. This text is LLM-facing system prompt content.
        ///
        /// Implementations may choose to filter or shape guidance based on the
        /// AgentExecuteRequest (e.g., mode, scope, tenant, etc.).
        /// </summary>
        /// <param name="modeKey">Current mode of the system.  This will be used to return the correct tools.</param>
        /// <returns>
        /// A single string containing delimited usage guidance for all
        /// registered tools.
        /// </returns>
        string GetToolUsageMetadata(string[] toolIds);

        string GetToolUsageMetadata(string toolName);
        string GetToolSummary(string toolName);
    }
}
