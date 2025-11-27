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
        [JsonProperty("type")]
        public string Type { get; set; } = "text";

        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public class ResponsesToolChoice
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "tool";

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
