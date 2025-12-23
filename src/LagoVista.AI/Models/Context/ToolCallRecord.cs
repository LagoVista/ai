using Newtonsoft.Json;

namespace LagoVista.AI.Models.Context
{
    /// <summary>
    /// AGN-030: A single tool call record stored in the ToolCallManifest register.
    ///
    /// The manifest is a Consumable register that captures:
    /// - tool name
    /// - call parameters
    /// - tool output
    /// - tool call reference/id
    /// </summary>
    public sealed class ToolCallRecord
    {
        /// <summary>
        /// Tool name (e.g., "session_kfr").
        /// </summary>
        [JsonProperty("toolName")]
        public string ToolName { get; set; } = string.Empty;

        /// <summary>
        /// JSON serialized parameters for the tool call.
        /// </summary>
        [JsonProperty("parametersJson")]
        public string ParametersJson { get; set; } = string.Empty;

        /// <summary>
        /// JSON serialized output for the tool call.
        /// </summary>
        [JsonProperty("outputJson")]
        public string OutputJson { get; set; } = string.Empty;

        /// <summary>
        /// Tool call reference/id used for correlation with the LLM tool-calling protocol.
        /// </summary>
        [JsonProperty("toolCallRef")]
        public string ToolCallRef { get; set; } = string.Empty;
    }
}
