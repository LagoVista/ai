using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;

namespace LagoVista.AI.Helpers
{
    /// <summary>
    /// Helper for constructing the request DTO to send to the OpenAI /responses API,
    /// following AGN-003 (Aptix Responses API Request Construction).
    ///
    /// Combines:
    /// - ConversationContext (model, system/boot prompt, temperature)
    /// - AgentExecuteRequest (mode, instruction, continuation id, tools, tool results)
    /// - ragContextBlock (pre-formatted [CONTEXT] block per AGN-002)
    /// </summary>
    public static class ResponsesRequestBuilder
    {
        /// <summary>
        /// Build the /responses request object as a strongly-typed DTO.
        /// The caller can serialize this with Json.NET and POST it to the API.
        /// </summary>
        /// <param name="conversationContext">The reasoning profile / boot prompt for this conversation.</param>
        /// <param name="request">The agent execute request from the client.</param>
        /// <param name="ragContextBlock">The pre-formatted RAG context block (may be null or empty).</param>
        /// <param name="stream">Whether to stream the response via SSE.</param>
        /// <returns>ResponsesApiRequest representing the body for the /responses call.</returns>
        public static ResponsesApiRequest Build(
            ConversationContext conversationContext,
            AgentExecuteRequest request,
            string ragContextBlock,
            bool? stream = null)
        {
            if (conversationContext == null) throw new ArgumentNullException(nameof(conversationContext));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var isContinuation = !string.IsNullOrWhiteSpace(request.ResponseContinuationId);

            var dto = new ResponsesApiRequest
            {
                Model = conversationContext.ModelName,
                Temperature = conversationContext.Temperature,
                Stream = stream
            };

            if (isContinuation)
            {
                dto.PreviousResponseId = request.ResponseContinuationId;
            }

            //
            // 1) Initial turn: include system / boot prompt from ConversationContext
            //
            if (!isContinuation)
            {
                var systemMessage = new ResponsesMessage
                {
                    Role = "system",
                    Content = new List<ResponsesMessageContent>
                    {
                        new ResponsesMessageContent
                        {
                            // Type defaults to "input_text"
                            Text = conversationContext.System ?? string.Empty
                        }
                    }
                };

                dto.Input.Add(systemMessage);
                Console.WriteLine($"-----\r\n{systemMessage}\r\n---\r\n\r\n");
            }

            


            //
            // 2) User message: [MODE] + [INSTRUCTION] (+ optional [CONTEXT])
            //
            var userMessage = new ResponsesMessage
            {
                Role = "user",
                Content = new List<ResponsesMessageContent>()
            };

            var instructionBlock = "[MODE: " + request.Mode + "]\n\n[INSTRUCTION]\n" + (request.Instruction ?? string.Empty);

            userMessage.Content.Add(new ResponsesMessageContent
            {
                Text = instructionBlock
            });

            Console.WriteLine($"-----\r\n{instructionBlock}\r\n---\r\n\r\n");

            if (!string.IsNullOrWhiteSpace(ragContextBlock))
            {
                userMessage.Content.Add(new ResponsesMessageContent
                {
                    Text = ragContextBlock
                });

                Console.WriteLine($"-----\r\n{ragContextBlock}\r\n---\r\n\r\n");
            }

            //
            // 3) Tool results (as plain text) if any were executed server-side.
            //
            // AgentReasoner populates request.ToolResultsJson with the serialized
            // collection of AgentToolCall objects that actually ran on the server.
            //
            // Here we build a "[TOOL_RESULTS]" text block that describes:
            // - which tools ran
            // - their inputs
            // - their outputs or error messages
            //
            if (!string.IsNullOrWhiteSpace(request.ToolResultsJson))
            {
                try
                {
                    var resultsArray = JArray.Parse(request.ToolResultsJson);
                    if (resultsArray.Count > 0)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("[TOOL_RESULTS]");
                        sb.AppendLine("The following server-side tools were executed for this turn:");
                        sb.AppendLine();

                        foreach (var token in resultsArray)
                        {
                            if (token is JObject obj)
                            {
                                var callId = obj.Value<string>("CallId") ?? obj.Value<string>("call_id");
                                var name = obj.Value<string>("Name") ?? obj.Value<string>("name");
                                var argumentsJson = obj.Value<string>("ArgumentsJson") ?? obj.Value<string>("arguments");
                                var wasExecuted = obj.Value<bool?>("WasExecuted") ?? false;
                                var isServerTool = obj.Value<bool?>("IsServerTool") ?? false;
                                var resultJson = obj.Value<string>("ResultJson");
                                var errorMessage = obj.Value<string>("ErrorMessage");

                                sb.AppendLine($"- Tool: {name ?? "(unknown)"}");
                                if (!string.IsNullOrWhiteSpace(callId))
                                {
                                    sb.AppendLine($"  CallId: {callId}");
                                }

                                sb.AppendLine($"  IsServerTool: {isServerTool}");
                                sb.AppendLine($"  WasExecuted: {wasExecuted}");

                                if (!string.IsNullOrWhiteSpace(argumentsJson))
                                {
                                    sb.AppendLine("  Arguments:");
                                    sb.AppendLine($"    {argumentsJson}");
                                }

                                if (!string.IsNullOrWhiteSpace(resultJson))
                                {
                                    sb.AppendLine("  ResultJson:");
                                    sb.AppendLine($"    {resultJson}");
                                }

                                if (!string.IsNullOrWhiteSpace(errorMessage))
                                {
                                    sb.AppendLine("  ErrorMessage:");
                                    sb.AppendLine($"    {errorMessage}");
                                }

                                sb.AppendLine();
                            }
                        }

                        userMessage.Content.Add(new ResponsesMessageContent
                        {
                            Text = sb.ToString()
                        });

                        Console.WriteLine($"-----\r\n{sb.ToString()}\r\n----\r\n");
                    }
                }
                catch (JsonException)
                {
                    // If ToolResultsJson is malformed, we skip the TOOL_RESULTS block.
                    // Upstream logging / diagnostics will still have the raw JSON.
                }
            }

            dto.Input.Add(userMessage);

            //
            // 4) Tools (only on initial turn)
            //
            if (!string.IsNullOrWhiteSpace(request.ToolsJson) && !isContinuation)
            {
                try
                {
                    var toolsToken = JToken.Parse(request.ToolsJson);
                    if (toolsToken is JArray toolsArray && toolsArray.Count > 0)
                    {
                        dto.Tools = new List<JObject>();
                        foreach (var tool in toolsArray)
                        {
                            if (tool is JObject obj)
                            {
                                dto.Tools.Add(obj);
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    // If ToolsJson is malformed, silently ignore here.
                    // Higher-level code can log this if desired.
                }
            }

            //
            // 5) Tool choice (if specified)
            //
            if (!string.IsNullOrWhiteSpace(request.ToolChoiceName))
            {
                dto.ToolChoice = new ResponsesToolChoice
                {
                    Name = request.ToolChoiceName
                };
            }

            return dto;
        }
    }
}
