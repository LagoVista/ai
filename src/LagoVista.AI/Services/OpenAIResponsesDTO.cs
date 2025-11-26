using System.Collections.Generic;
using Newtonsoft.Json;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// POCO for the /v1/responses REST API (non-streaming).
    /// </summary>
    public sealed class ResponsesApiResponse
    {
        /// <summary>
        /// The unique ID of the response, e.g. "resp_abc123".
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// Always "response".
        /// </summary>
        [JsonProperty("object")]
        public string Object { get; set; }

        /// <summary>
        /// Unix timestamp (seconds since epoch).
        /// </summary>
        [JsonProperty("created_at")]
        public double CreatedAt { get; set; }

        /// <summary>
        /// Response status, e.g. "completed".
        /// </summary>
        [JsonProperty("status")]
        public string Status { get; set; }

        /// <summary>
        /// Model name, e.g. "gpt-4.1" / "gpt-4o".
        /// </summary>
        [JsonProperty("model")]
        public string Model { get; set; }

        /// <summary>
        /// Top-level output items (messages, reasoning items, tool calls, etc.).
        /// For simple text calls, this will usually contain one "message" item
        /// with role "assistant".
        /// </summary>
        [JsonProperty("output")]
        public List<ResponsesOutputItem> Output { get; set; }

        /// <summary>
        /// Convenience field that some SDKs expose as response.output_text.
        /// If present in the JSON, this will be the concatenated assistant text.
        /// It's safe to treat this as optional and fall back to Output[..].Content[..].Text.
        /// </summary>
        [JsonProperty("output_text")]
        public string OutputText { get; set; }

        [JsonProperty("usage")]
        public ResponsesUsage Usage { get; set; }

        [JsonProperty("instructions")]
        public string Instructions { get; set; }

        [JsonProperty("max_output_tokens")]
        public int? MaxOutputTokens { get; set; }

        [JsonProperty("parallel_tool_calls")]
        public bool? ParallelToolCalls { get; set; }

        [JsonProperty("previous_response_id")]
        public string PreviousResponseId { get; set; }

        [JsonProperty("store")]
        public bool? Store { get; set; }

        [JsonProperty("temperature")]
        public double? Temperature { get; set; }

        [JsonProperty("tool_choice")]
        public object ToolChoice { get; set; }

        [JsonProperty("tools")]
        public IList<object> Tools { get; set; }

        [JsonProperty("top_p")]
        public double? TopP { get; set; }

        [JsonProperty("truncation")]
        public string Truncation { get; set; }

        [JsonProperty("metadata")]
        public Dictionary<string, object> Metadata { get; set; }

        [JsonProperty("user")]
        public string User { get; set; }

        [JsonProperty("background")]
        public bool? Background { get; set; }

        [JsonProperty("error")]
        public ResponsesError Error { get; set; }

        [JsonProperty("incomplete_details")]
        public ResponsesIncompleteDetails IncompleteDetails { get; set; }

        // Reasoning, text config, etc., can be added as needed.
    }

    /// <summary>
    /// One item in the "output" array.
    /// For normal text responses, Type = "message", Role = "assistant".
    /// </summary>
    public sealed class ResponsesOutputItem
    {
        [JsonProperty("type")]
        public string Type { get; set; }  // e.g. "message", "reasoning"

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; } // e.g. "completed"

        [JsonProperty("role")]
        public string Role { get; set; }   // e.g. "assistant", "user"

        /// <summary>
        /// For message items, this is an array of content parts.
        /// For plain text, you'll typically see a single part with Type="output_text".
        /// </summary>
        [JsonProperty("content")]
        public List<ResponsesContentPart> Content { get; set; }
    }

    /// <summary>
    /// One content part inside an output item.
    /// For plain text, Type = "output_text" and Text is the actual text.
    /// </summary>
    public sealed class ResponsesContentPart
    {
        [JsonProperty("type")]
        public string Type { get; set; } // e.g. "output_text"

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("annotations")]
        public List<object> Annotations { get; set; }
    }

    /// <summary>
    /// Token usage information.
    /// </summary>
    public sealed class ResponsesUsage
    {
        [JsonProperty("input_tokens")]
        public int InputTokens { get; set; }

        [JsonProperty("output_tokens")]
        public int OutputTokens { get; set; }

        [JsonProperty("total_tokens")]
        public int TotalTokens { get; set; }

        [JsonProperty("input_tokens_details")]
        public ResponsesInputTokensDetails InputTokensDetails { get; set; }

        [JsonProperty("output_tokens_details")]
        public ResponsesOutputTokensDetails OutputTokensDetails { get; set; }
    }

    public sealed class ResponsesInputTokensDetails
    {
        [JsonProperty("cached_tokens")]
        public int CachedTokens { get; set; }
    }

    public sealed class ResponsesOutputTokensDetails
    {
        [JsonProperty("reasoning_tokens")]
        public int ReasoningTokens { get; set; }
    }

    /// <summary>
    /// Error object (when status != completed).
    /// </summary>
    public sealed class ResponsesError
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    /// <summary>
    /// Present if the response is incomplete.
    /// </summary>
    public sealed class ResponsesIncompleteDetails
    {
        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("reason_code")]
        public string ReasonCode { get; set; }
    }
}
