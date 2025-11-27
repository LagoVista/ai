using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Helpers
{
    /// <summary>
    /// Static helper to convert a raw OpenAI /responses JSON payload into an AgentExecuteResponse
    /// according to AGN-004.
    /// </summary>
    public static class AgentExecuteResponseParser
    {
        /// <summary>
        /// Parses the raw JSON string returned by the OpenAI /responses API into an AgentExecuteResponse.
        /// Conversation/agent/context identifiers and mode are taken from the AgentExecuteRequest.
        /// </summary>
        /// <param name="rawJson">Raw JSON from the /responses call.</param>
        /// <param name="request">The AgentExecuteRequest used to initiate this call.</param>
        /// <returns>Populated AgentExecuteResponse.</returns>
        public static InvokeResult<AgentExecuteResponse> Parse(string rawJson, AgentExecuteRequest request)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return InvokeResult<AgentExecuteResponse>.FromError("[AgentExecuteResponseParser__Parse] Empty or null JSON payload.");
            }

            JObject root;
            try
            {
                root = JObject.Parse(rawJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine(rawJson);
                return InvokeResult<AgentExecuteResponse>.FromError($"[AgentExecuteResponseParser__Parse] {ex.Message}");
            }

            var response = new AgentExecuteResponse
            {
                RawResponseJson = rawJson,
                ConversationId = request?.ConversationId,
                ConversationContextId = request?.ConversationContext?.Id,
                AgentContextId = request?.AgentContext?.Id,
                Mode = request?.Mode
            };

            // ID + threading
            response.ResponseContinuationId = root.Value<string>("id");
            response.TurnId = response.ResponseContinuationId;

            // Model
            response.ModelId = root.Value<string>("model");

            // Usage block
            var usage = root["usage"];
            if (usage != null)
            {
                response.Usage.PromptTokens = usage.Value<int?>("prompt_tokens") ?? 0;
                response.Usage.CompletionTokens = usage.Value<int?>("completion_tokens") ?? 0;
                response.Usage.TotalTokens = usage.Value<int?>("total_tokens") ?? 0;
            }

            // Extract output array
            var outputArray = root["output"] as JArray;
            if (outputArray == null)
            {
                return InvokeResult<AgentExecuteResponse>.FromError("[AgentExecuteResponseParser__Parse] Missing [output] Node.");
            }

            var textSegments = new List<string>();
            var toolCalls = new List<AgentToolCall>();
            var finishReasons = new List<string>();

            foreach (var item in outputArray)
            {
                var type = item.Value<string>("type");

                switch (type)
                {
                    case "output_text":
                    {
                        var txt = item.Value<string>("text");
                        if (!string.IsNullOrWhiteSpace(txt))
                        {
                            textSegments.Add(txt);
                        }

                        var fr = item.Value<string>("finish_reason");
                        if (!string.IsNullOrWhiteSpace(fr))
                        {
                            finishReasons.Add(fr);
                        }
                        break;
                    }

                    case "tool_call":
                    {
                        var call = item["tool_call"];
                        if (call != null)
                        {
                            var agentCall = new AgentToolCall
                            {
                                CallId = call.Value<string>("id"),
                                Name = call.Value<string>("name"),
                                ArgumentsJson = call["arguments"]?.ToString(Formatting.None)
                            };

                            toolCalls.Add(agentCall);

                            var fr = call.Value<string>("finish_reason");
                            if (!string.IsNullOrWhiteSpace(fr))
                            {
                                finishReasons.Add(fr);
                            }
                        }
                        break;
                    }

                    default:
                    {
                        var fr = item.Value<string>("finish_reason");
                        if (!string.IsNullOrWhiteSpace(fr))
                        {
                            finishReasons.Add(fr);
                        }
                        break;
                    }
                }
            }

            // Aggregate text
            response.Text = textSegments.Count > 0
                ? string.Join("\n\n", textSegments)
                : null;

            // Tool calls
            response.ToolCalls = toolCalls;

            // Finish reason (use the last one seen if multiple)
            response.FinishReason = finishReasons.Count > 0
                ? finishReasons.Last()
                : null;

            // Classify Kind
            if (!string.IsNullOrWhiteSpace(response.ErrorCode) || !string.IsNullOrWhiteSpace(response.ErrorMessage))
            {
                return InvokeResult<AgentExecuteResponse>.FromError($"{response.ErrorCode} : {response.ErrorMessage}");
            }
            else if (response.ToolCalls.Any() && string.IsNullOrWhiteSpace(response.Text))
            {
                response.Kind = "tool-only";
            }
            else if (string.IsNullOrWhiteSpace(response.Text) && !response.ToolCalls.Any())
            {
                return InvokeResult<AgentExecuteResponse>.FromError($"[AgentExecuteResponseParser__Parse] - No tool or text response.");
            }
            else
            {
                response.Kind = "ok";
            }

            return InvokeResult<AgentExecuteResponse>.Create(response);
        }
    }
}
