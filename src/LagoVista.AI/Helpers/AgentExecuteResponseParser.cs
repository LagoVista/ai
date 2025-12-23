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
                SessionId = request?.SessionId,
                ConversationContextId = request?.ConversationContext?.Id,
                AgentContextId = request?.AgentContext?.Id,
                Mode = request?.Mode
            };

            // ID + threading
            response.ResponseContinuationId = root.Value<string>("id");
            response.TurnId = response.ResponseContinuationId;

            // Model
            response.ModelId = root.Value<string>("model");

            // Usage block – support both old (prompt/completion) and new (input/output) fields
            var usage = root["usage"];
            if (usage != null)
            {
                var promptTokens =
                    usage.Value<int?>("prompt_tokens") ??
                    usage.Value<int?>("input_tokens");

                var completionTokens =
                    usage.Value<int?>("completion_tokens") ??
                    usage.Value<int?>("output_tokens");

                var totalTokens =
                    usage.Value<int?>("total_tokens");

                response.Usage.PromptTokens = promptTokens ?? 0;
                response.Usage.CompletionTokens = completionTokens ?? 0;
                response.Usage.TotalTokens = totalTokens ?? (response.Usage.PromptTokens + response.Usage.CompletionTokens);
            }

            // Extract output – allow both array and single-object shapes
            var outputToken = root["output"];
            JArray outputArray = null;

            if (outputToken is JArray arr)
            {
                outputArray = arr;
            }
            else if (outputToken is JObject singleObj)
            {
                outputArray = new JArray(singleObj);
            }

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
                            // Legacy flat shape: output[] items are output_text
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
                            // Legacy flat tool_call shape
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

                    case "function_call":
                        {
                            // New Responses shape for tool calls in output[]
                            // Example:
                            // {
                            //   "type": "function_call",
                            //   "status": "completed",
                            //   "arguments": "{\"message\":\"hello\",\"count\":0}",
                            //   "call_id": "call_...",
                            //   "name": "testing_ping_pong"
                            // }

                            string argsJson = null;
                            var argsToken = item["arguments"];
                            if (argsToken != null)
                            {
                                if (argsToken.Type == JTokenType.String)
                                {
                                    var rawArgs = argsToken.Value<string>();
                                    if (!string.IsNullOrWhiteSpace(rawArgs))
                                    {
                                        try
                                        {
                                            argsJson = JToken.Parse(rawArgs).ToString(Formatting.None);
                                        }
                                        catch
                                        {
                                            // If it's not valid JSON, just keep the raw string.
                                            argsJson = rawArgs;
                                        }
                                    }
                                }
                                else
                                {
                                    argsJson = argsToken.ToString(Formatting.None);
                                }
                            }

                            var agentCall = new AgentToolCall
                            {
                                CallId = item.Value<string>("call_id"),
                                Name = item.Value<string>("name"),
                                ArgumentsJson = argsJson
                            };

                            toolCalls.Add(agentCall);

                            var fr = item.Value<string>("finish_reason");
                            if (!string.IsNullOrWhiteSpace(fr))
                            {
                                finishReasons.Add(fr);
                            }

                            break;
                        }

                    case "message":
                        {
                            // New Responses shape: output[] contains message objects with content[]
                            var contentArray = item["content"] as JArray;
                            if (contentArray != null)
                            {
                                foreach (var content in contentArray)
                                {
                                    var contentType = content.Value<string>("type");

                                    if (string.Equals(contentType, "output_text", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var txt = content.Value<string>("text");
                                        if (!string.IsNullOrWhiteSpace(txt))
                                        {
                                            textSegments.Add(txt);
                                        }
                                    }
                                    else if (string.Equals(contentType, "tool_call", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var call = content["tool_call"];
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
                                    }
                                }
                            }

                            var frMsg = item.Value<string>("finish_reason");
                            if (!string.IsNullOrWhiteSpace(frMsg))
                            {
                                finishReasons.Add(frMsg);
                            }

                            break;
                        }

                    default:
                        {
                            // e.g. "reasoning" items or other types we don't currently surface
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
                return InvokeResult<AgentExecuteResponse>.FromError("[AgentExecuteResponseParser__Parse] - No tool or text response.");
            }
            else
            {
                response.Kind = "ok";
            }

            return InvokeResult<AgentExecuteResponse>.Create(response);
        }
    }
}
