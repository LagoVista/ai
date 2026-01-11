using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LagoVista.Core.Validation;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using System.Threading.Tasks;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.AI.Interfaces.Repos;

namespace LagoVista.AI.Helpers
{
    /// <summary>
    /// Static helper to convert a raw OpenAI /responses JSON payload into an AgentExecuteResponse
    /// according to AGN-004.
    /// </summary>
    public class AgentExecuteResponseParser : IAgentExecuteResponseParser
    {
        private readonly IAdminLogger _logger;
        private readonly IAgentTurnChatHistoryRepo _turnHistoryRepo;
        private readonly IAgentTurnTranscriptStore _transcriptStore;

        public AgentExecuteResponseParser(IAdminLogger logger, IAgentTurnTranscriptStore transcriptStore, IAgentTurnChatHistoryRepo turnHistoryRepo)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _transcriptStore = transcriptStore ?? throw new ArgumentNullException(nameof(transcriptStore));
            _turnHistoryRepo = turnHistoryRepo ?? throw new ArgumentNullException(nameof(turnHistoryRepo));
        }

        /// <summary>
        /// Parses the raw JSON string returned by the OpenAI /responses API into an AgentExecuteResponse.
        /// Conversation/agent/context identifiers and mode are taken from the AgentExecuteRequest.
        /// </summary>
        /// <param name="rawJson">Raw JSON from the /responses call.</param>
        /// <param name="request">The AgentExecuteRequest used to initiate this call.</param>
        /// <returns>Populated AgentExecuteResponse.</returns>
        public async Task<InvokeResult<IAgentPipelineContext>> ParseAsync(IAgentPipelineContext ctx, string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return InvokeResult<IAgentPipelineContext>.FromError("[AgentExecuteResponseParser__Parse] Empty or null JSON payload.");
            }

            JObject root;
            try
            {
                root = JObject.Parse(rawJson);
           
                var response = new ResponsePayload
                {

                };


                var id = root["id"];
                if (id == null)
                {
                    return InvokeResult<IAgentPipelineContext>.FromError("[AgentExecuteResponseParser__Parse] Missing [id] Node.");
                }
                else
                {
                    ctx.ThisTurn.OpenAIResponseId = id.Value<string>();
                }

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

                    var promptTokenDetails = usage["prompt_tokens_details"];
                    if (promptTokenDetails != null)
                    {
                        var cachedTokens = promptTokenDetails.Value<int?>("cached_tokens");
                    }

                    var completionTokenDetails = usage["prompt_tokens_details"];
                    if (completionTokenDetails != null)
                    {
                        var reasoningTokens = completionTokenDetails.Value<int?>("reasoning_tokens");
                        response.Usage.ReasoningTokens = reasoningTokens ?? 0;
                    }
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
                    return InvokeResult<IAgentPipelineContext>.FromError("[AgentExecuteResponseParser__Parse] Missing [output] Node.");
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
                                        ToolCallId = call.Value<string>("id"),
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
                                    ToolCallId = item.Value<string>("call_id"),
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
                                                    ToolCallId = call.Value<string>("id"),
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

                response.PrimaryOutputText = textSegments.Count > 0 ? string.Join("\n\n", textSegments) : null;

                ctx.ThisTurn.PromptTokens = response.Usage.PromptTokens;
                ctx.ThisTurn.CachedTokens = response.Usage.CachedTokends;
                ctx.ThisTurn.TotalTokens = response.Usage.TotalTokens;
                ctx.ThisTurn.ReasoningTokens = response.Usage.ReasoningTokens;
                ctx.ThisTurn.CompletionTokens = response.Usage.CompletionTokens;

                ctx.ThisTurn.OpenAIResponseBlobUrl = (await _transcriptStore.SaveTurnResponseAsync(ctx.Envelope.Org.Id, ctx.Session.Id, ctx.ThisTurn.Id, rawJson.Trim(), ctx.CancellationToken)).Result.ToString();

                var lastIteration = ctx.ThisTurn.Iterations.LastOrDefault();

                var iteration = new AgentSessionTurnIteration()
                {
                    Index = ctx.ThisTurn.Iterations.Count + 1,
                    PromptTokens = response.Usage.PromptTokens,
                    OpenAIResponseBlobUrl = ctx.ThisTurn.OpenAIResponseBlobUrl,
                    OpenAIRequestBlobUrl = ctx.ThisTurn.OpenAIRequestBlobUrl,
                    OpenAiResponseId = ctx.ThisTurn.OpenAIResponseId,
                    CachedTokens = response.Usage.CachedTokends,
                    TotalTokens = response.Usage.TotalTokens,
                    ReasoningTokens = response.Usage.ReasoningTokens,
                    CompletionTokens = response.Usage.CompletionTokens,
                    Status =  Core.Models.EntityHeader<AgentSessionTurnStatuses>.Create(AgentSessionTurnStatuses.Completed),
                    ToolCalls = ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCalls.Select(tc => tc.Name).ToList()
                };

                ctx.ThisTurn.Iterations.Add(iteration);

                if (String.IsNullOrEmpty(response.PrimaryOutputText) && ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCalls.Count > 0)
                    ctx.ThisTurn.AgentAnswerSummary = "tool call - likely will be replaced by final output";
                else if (!String.IsNullOrEmpty(response.PrimaryOutputText))
                {
                    var finalOutput = response.PrimaryOutputText;
                    
                    if(finalOutput.Contains("APTIX-PLAN-END"))
                        finalOutput = finalOutput.Substring(finalOutput.IndexOf("APTIX-PLAN-END") + "APTIX-PLAN-END".Length).Trim();

                    var userInput = string.IsNullOrEmpty(ctx.Envelope.Instructions) ? "file upload" : ctx.Envelope.Instructions;
                    await _turnHistoryRepo.AppendTurnAsync(ctx.Envelope.Org.Id, ctx.Session.Id, ctx.ThisTurn.Id, userInput, finalOutput, ctx.CancellationToken);

                    ctx.ThisTurn.InstructionsTruncated = userInput.Length > 4 * 1024;
                    ctx.ThisTurn.AgentAnswerTruncated = finalOutput.Length > 4 * 1024;
    
                    if(ctx.ThisTurn.InstructionsTruncated)
                        userInput = userInput.Substring(0, 4 * 1024);
                    
                    if (ctx.ThisTurn.AgentAnswerTruncated)
                        finalOutput = finalOutput.Substring(0, 4 * 1024);

                    ctx.ThisTurn.InstructionSummary = userInput;
                    ctx.ThisTurn.AgentAnswerSummary = finalOutput;
                }
                else
                    InvokeResult<IAgentPipelineContext>.FromError("[AgentExecuteResponseParser__Parse] No output text or tool calls found in response.");

                if (toolCalls.Any())
                    ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCalls = toolCalls;
                else
                {
                    ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCalls.Clear();
                    ctx.SetResponsePayload(response);
                }

                return InvokeResult<IAgentPipelineContext>.Create(ctx);
            }
            catch (Exception ex)
            {
                _logger.AddException("[AgentExecuteResponseParser__Parse]", ex);
                return InvokeResult<IAgentPipelineContext>.FromError($"[AgentExecuteResponseParser__Parse] {ex.Message}");
            }
        }
    }
}
