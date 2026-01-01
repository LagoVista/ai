using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using LagoVista.Core.Models.ML;
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
        public List<ResponsesInputItem> Input { get; set; } = new List<ResponsesInputItem>();

        /// <summary>Z
        /// Tools passed to /responses; kept as raw JSON objects so the client
        /// (VS Code extension) can define them freely.
        /// </summary>
        [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
        public List<OpenAiToolDefinition> Tools { get; set; } = new List<OpenAiToolDefinition>();

        /// <summary>
        /// Optional tool choice hint (e.g., { type: "tool", name: "ddr_document" }).
        /// </summary>
        [JsonProperty("tool_choice", NullValueHandling = NullValueHandling.Ignore)]
        public ResponsesToolChoice ToolChoice { get; set; }

        /// <summary>
        /// Should partial results be streamed.
        /// </summary>
        [JsonProperty("stream")]
        public bool Stream { get; set; }
    }

    public class ResponsesMessage
    {
        public ResponsesMessage(string role)
        {
            Role = role;
        }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public List<ResponsesMessageContent> Content { get; set; } = new List<ResponsesMessageContent>();
    }

    public abstract class ResponsesInputItem
    {
        [JsonProperty("type")]
        public abstract string Type { get; }
    }

    // normal message item
    public sealed class ResponsesInputMessage : ResponsesInputItem
    {
        public ResponsesInputMessage(string role)
        {
            Role = role;
        }

        public override string Type => "message";

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public List<ResponsesMessageContent> Content { get; set; } = new List<ResponsesMessageContent>();
    }

    // tool output item
    public sealed class ResponsesFunctionCallOutput : ResponsesInputItem
    {
        public override string Type => "function_call_output";

        [JsonProperty("call_id")]
        public string CallId { get; set; }

        // usually a string (often JSON)
        [JsonProperty("output")]
        public string Output { get; set; }
    }

    public class ResponsesMessageContent
    {
        /// <summary>
        /// Output type; for our usage we keep this as "input_text".
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; } = "input_text";

        /// <summary>
        /// Plain text content (system prompt, instruction, RAG context, tool summaries, etc.).
        /// </summary>
        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }

        // NOTE: plain string, not nested object
        [JsonProperty("image_url", NullValueHandling = NullValueHandling.Ignore)]
        public string ImageUrl { get; set; }
        
        [JsonProperty("mime_type", NullValueHandling = NullValueHandling.Ignore)]
        public string MimeType { get; set; }
    }


    public class ResponsesImageUrl
    {
        [JsonProperty("url")]
        public string Url { get; set; }
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

    public sealed class OpenAIToolResultRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("input")]
        public List<OpenAIToolResult> Inputs { get; set; } = new List<OpenAIToolResult>();
    
        public static OpenAIToolResultRequest FromResults(List<AgentToolCallResult> results)
        {
            var request = new OpenAIToolResultRequest();
            foreach (var result in results)
            {
                request.Inputs.Add(OpenAIToolResult.FromResult(result));
            }
            return request;
        }
    }

   
    public sealed class OpenAIToolResult
    {
        [JsonProperty("type")]
        public string Type { get; } = "custom_tool_call_output";

        [JsonProperty("call_id")]
        public string CallId { get; set; }

        [JsonProperty("output")]
        public List<OpenAIToolResultOutput> Output { get; set; }

        public static OpenAIToolResult FromResult(AgentToolCallResult result)
        {
            if (String.IsNullOrEmpty(result.ToolCallId))
                throw new InvalidOperationException("Missing tool call id");

            var toolResult = new OpenAIToolResult
            {
                CallId = result.ToolCallId,
                Output = new List<OpenAIToolResultOutput>() {  new OpenAIToolResultOutput() {  Text = result.ResultJson} }
            };
            return toolResult;
        }
    }


    public sealed class OpenAIToolResultOutput
    {
        [JsonProperty("type")]
        public string Type { get; } = "input_text";

        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public sealed class OpenAIToolFinalResult
    {
        [JsonProperty("status")]
        public string Status
        {
            get
            {
                if (Result != null)
                    return "ok";

                if (Error != null)
                    return "error";

                throw new InvalidOperationException("Tool result did not have error or result");
            }
        }

        [JsonProperty("result")]
        public JToken Result { get; set; }

        [JsonProperty("error")]
        public OpenAIToolResultError Error { get; set; }
    }


    public sealed class OpenAIToolResultError
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("retryable")]
        public bool Retryable { get; set; }
    }
}
