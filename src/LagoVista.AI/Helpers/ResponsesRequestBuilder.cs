using System;
using System.Collections.Generic;
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
                Stream = stream,
            };

            if (isContinuation)
            {
                dto.PreviousResponseId = request.ResponseContinuationId;
            }

            //
            // INPUT MESSAGES
            //

            // 1) Initial turn: include system / boot prompt from ConversationContext
            if (!isContinuation)
            {
                var systemMessage = new ResponsesMessage
                {
                    Role = "system",
                    Content = new List<ResponsesMessageContent>
                    {
                        new ResponsesMessageContent
                        {
                            // Type defaults to "input_text" via DTO
                            Text = conversationContext.System ?? string.Empty
                        }
                    }
                };

                dto.Input.Add(systemMessage);
            }

            // 2) User message: [MODE] + [INSTRUCTION] (+ optional [CONTEXT] and [TOOL_RESULTS])
            var userMessage = new ResponsesMessage
            {
                Role = "user",
                Content = new List<ResponsesMessageContent>()
            };

            var instructionBlock =
                "[MODE: " + request.Mode + "]\n\n[INSTRUCTION]\n" + (request.Instruction ?? string.Empty);

            // Primary instruction
            userMessage.Content.Add(new ResponsesMessageContent
            {
                Text = instructionBlock
            });

            // Optional RAG context block
            if (!string.IsNullOrWhiteSpace(ragContextBlock))
            {
                userMessage.Content.Add(new ResponsesMessageContent
                {
                    Text = ragContextBlock
                });
            }

            // Optional TOOL_RESULTS block as additional input_text
            if (!string.IsNullOrWhiteSpace(request.ToolResultsJson))
            {
                var toolResultsText = ToolResultsTextBuilder.BuildFromToolResultsJson(request.ToolResultsJson);
                if (!string.IsNullOrWhiteSpace(toolResultsText))
                {
                    userMessage.Content.Add(new ResponsesMessageContent
                    {
                        Text = toolResultsText
                    });
                }
            }

            dto.Input.Add(userMessage);

            //
            // 3) Tools (only on initial turn)
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
                            var obj = tool as JObject;
                            if (obj != null)
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
            // 4) Tool choice (if specified)
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
