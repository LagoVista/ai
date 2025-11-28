using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Models
{
    /// <summary>
    /// Strongly-typed request DTO for the OpenAI /responses API,
    /// as constructed by the Aptix orchestrator.
    ///
    /// This mirrors only the fields we actively use (AGN-003),
    /// not necessarily the complete OpenAI schema.
    /// </summary>
    public class ResponsesApiRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("temperature")]
        public float Temperature { get; set; }

        [JsonProperty("previous_response_id", NullValueHandling = NullValueHandling.Ignore)]
        public string PreviousResponseId { get; set; }

        [JsonProperty("input")]
        public List<ResponsesMessage> Input { get; set; } = new List<ResponsesMessage>();

        /// <summary>
        /// Tools passed to /responses; kept as raw JSON objects so the client
        /// (VS Code extension) can define them freely.
        /// </summary>
        [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
        public List<JObject> Tools { get; set; }

        /// <summary>
        /// Optional tool choice hint (e.g., { type: "tool", name: "ddr_document" }).
        /// </summary>
        [JsonProperty("tool_choice", NullValueHandling = NullValueHandling.Ignore)]
        public ResponsesToolChoice ToolChoice { get; set; }

        /// <summary>
        /// Should partial results be streamed.
        /// </summary>
        [JsonProperty("stream")]
        public bool? Stream { get; set; }
    }

    public class ResponsesMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public List<ResponsesMessageContent> Content { get; set; } = new List<ResponsesMessageContent>();
    }

    public class ResponsesMessageContent
    {
        /// <summary>
        /// Content type; for our usage we keep this as "input_text".
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; } = "input_text";

        /// <summary>
        /// Plain text content (system prompt, instruction, RAG context, tool summaries, etc.).
        /// </summary>
        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }
    }

    public class ResponsesToolChoice
    {
        /// <summary>
        /// For function selection, OpenAI expects "tool" here.
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; } = "tool";

        /// <summary>
        /// Name of the tool that must be used.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
