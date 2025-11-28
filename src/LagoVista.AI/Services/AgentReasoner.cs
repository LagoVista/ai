using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// Default implementation of IAgentReasoner.
    ///
    /// Implements a simple loop:
    /// 1) Call ILLMClient.GetAnswerAsync(...)
    /// 2) If no ToolCalls => return final result.
    /// 3) Execute any server-side tools via IAgentToolExecutor.
    /// 4) If any non-server (client) tools remain => return response so the
    ///    client can fulfill them.
    /// 5) If only server tools existed and succeeded => feed their results
    ///    back into the LLM via ToolResultsJson and repeat.
    /// </summary>
    public class AgentReasoner : IAgentReasoner
    {
        private readonly ILLMClient _llmClient;
        private readonly IAgentToolExecutor _toolExecutor;
        private readonly IAdminLogger _logger;

        // Safety cap to avoid runaway tool-trigger loops.
        private const int MaxReasoningIterations = 4;

        public AgentReasoner(ILLMClient llmClient, IAgentToolExecutor toolExecutor, IAdminLogger logger)
        {
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<InvokeResult<AgentExecuteResponse>> ExecuteAsync(
            AgentContext agentContext,
            ConversationContext conversationContext,
            AgentExecuteRequest request,
            string ragContextBlock,
            string sessionId,
            EntityHeader org,
            EntityHeader user,
            CancellationToken cancellationToken = default)
        {
            if (agentContext == null) throw new ArgumentNullException(nameof(agentContext));
            if (conversationContext == null) throw new ArgumentNullException(nameof(conversationContext));
            if (request == null) throw new ArgumentNullException(nameof(request));

            AgentExecuteResponse lastResponse = null;

            for (var iteration = 0; iteration < MaxReasoningIterations; iteration++)
            {
                _logger.Trace(
                    $"[AgentReasoner_ExecuteAsync] Iteration {iteration + 1} starting. " +
                    $"sessionId={sessionId}, mode={request.Mode}, conversationId={request.ConversationId}");

                var llmResult = await _llmClient.GetAnswerAsync(
                    agentContext,
                    conversationContext,
                    request,
                    ragContextBlock,
                    sessionId,
                    cancellationToken);

                if (!llmResult.Successful)
                {
                    _logger.AddError("[AgentReasoner_ExecuteAsync__LLMFailed]",
                        $"LLM call failed on iteration {iteration + 1}: {llmResult.ErrorMessage}");
                    return llmResult;
                }

                lastResponse = llmResult.Result;
                if (lastResponse == null)
                {
                    const string nullMsg = "LLM returned a null AgentExecuteResponse.";
                    _logger.AddError("[AgentReasoner_ExecuteAsync__NullResponse]", nullMsg);
                    return InvokeResult<AgentExecuteResponse>.FromError(nullMsg);
                }

                // If there are no tool calls, we are done.
                if (lastResponse.ToolCalls == null || lastResponse.ToolCalls.Count == 0)
                {
                    _logger.Trace(
                        "[AgentReasoner_ExecuteAsync] No tool calls detected. " +
                        "Returning final LLM response.");
                    return llmResult;
                }

                _logger.Trace(
                    $"[AgentReasoner_ExecuteAsync] Detected {lastResponse.ToolCalls.Count} tool call(s) " +
                    $"from LLM on iteration {iteration + 1}.");

                // Build a context for all server-side tools.
                var toolContext = new AgentToolExecutionContext
                {
                    AgentContext = agentContext,
                    ConversationContext = conversationContext,
                    Request = request,
                    SessionId = sessionId,
                    Org = org,
                    User = user
                };

                var executedServerCalls = new List<AgentToolCall>();
                var pendingClientCalls = new List<AgentToolCall>();

                foreach (var toolCall in lastResponse.ToolCalls)
                {
                    // Let the executor decide if this is a server tool or not.
                    var updatedCallResponse = await _toolExecutor.ExecuteServerToolAsync(
                        toolCall,
                        toolContext,
                        cancellationToken);

                    if(!updatedCallResponse.Successful) 
                        return InvokeResult < AgentExecuteResponse >.FromInvokeResult(updatedCallResponse.ToInvokeResult());

                    var updatedCall = updatedCallResponse.Result;

                    if (updatedCall.IsServerTool && updatedCall.WasExecuted)
                    {
                        executedServerCalls.Add(updatedCall);
                    }
                    else if (!updatedCall.IsServerTool)
                    {
                        // Not a server tool => leave for client execution.
                        pendingClientCalls.Add(updatedCall);
                    }
                    else
                    {
                        // IsServerTool == true but WasExecuted == false
                        // (e.g., error or cancellation). We keep it in the
                        // list so the caller can see the error, but we do not
                        // retry it on the client.
                        executedServerCalls.Add(updatedCall);
                    }
                }

                var toolOutputs = executedServerCalls
                    .Where(c => c.IsServerTool && c.WasExecuted && !string.IsNullOrWhiteSpace(c.CallId))
                    .Select(c => new ResponsesToolOutput
                    {
                        ToolCallId = c.CallId,
                        Output = string.IsNullOrWhiteSpace(c.ResultJson) ? "{}" : c.ResultJson
                    })
                    .ToList();

                                request.ToolResultsJson = JsonConvert.SerializeObject(toolOutputs);

                // If there are any client tools, we stop here.
                if (pendingClientCalls.Count > 0)
                {
                    _logger.Trace(
                        "[AgentReasoner_ExecuteAsync] Client tools detected. " +
                        "Returning response with mixed server/client tool calls.");

                    var mergedCalls = new List<AgentToolCall>();
                    mergedCalls.AddRange(executedServerCalls);
                    mergedCalls.AddRange(pendingClientCalls);

                    lastResponse.ToolCalls = mergedCalls;

                    return InvokeResult<AgentExecuteResponse>.Create(lastResponse);
                }

                // Only server tools were requested. If none executed successfully,
                // there's nothing more we can do; return the last response as-is.
                if (executedServerCalls.Count == 0)
                {
                    _logger.Trace(
                        "[AgentReasoner_ExecuteAsync] Only server tools requested, but none executed. " +
                        "Returning last response as-is.");
                    return InvokeResult<AgentExecuteResponse>.Create(lastResponse);
                }

                // At this point: all tool calls are server tools, and at least one executed.
                // Feed the server tool results back into the LLM via the request.
                _logger.Trace(
                    $"[AgentReasoner_ExecuteAsync] Executed {executedServerCalls.Count} server-side tool(s). " +
                    "Preparing to call LLM again with tool results.");

                try
                {
                    // Keep the strongly typed results (optional convenience).
                    request.ToolResults = executedServerCalls;

                    // JSON payload that ResponsesRequestBuilder should inject into the
                    // /responses request as tool outputs.
                    request.ToolResultsJson = JsonConvert.SerializeObject(executedServerCalls);
                }
                catch (Exception ex)
                {
                    _logger.AddException(
                        "[AgentReasoner_ExecuteAsync__ToolResultsSerializeException]",
                        ex);

                    var msg = $"Failed to serialize tool results for LLM follow-up call: {ex.Message}";
                    return InvokeResult<AgentExecuteResponse>.FromError(msg);
                }

                //// Carry forward the response continuation id if present so the
                //// LLM can "continue" the prior response.
                //if (!string.IsNullOrWhiteSpace(lastResponse.ResponseContinuationId))
                //{
                //    request.ResponseContinuationId = lastResponse.ResponseContinuationId;
                //}

                // IMPORTANT CHANGE:
                // We do NOT propagate the previous response id when feeding back tool results
                // as plain text. Doing so would cause the Responses API to expect structured
                // tool outputs for the earlier function_call ids.
                request.ResponseContinuationId = null;

                // Loop back to call the LLM again, now with tool results.
            }

            // If we reach here, we hit the max iteration safety cap.
            if (lastResponse != null)
            {
                const string warning = "Maximum reasoning iterations reached in AgentReasoner.";
                _logger.AddError("[AgentReasoner_ExecuteAsync__MaxIterations]", warning);

                if (lastResponse.Warnings == null)
                {
                    lastResponse.Warnings = new List<string>();
                }

                lastResponse.Warnings.Add(warning);

                return InvokeResult<AgentExecuteResponse>.Create(lastResponse);
            }

            const string noResponseMsg = "AgentReasoner completed without producing any LLM response.";
            _logger.AddError("[AgentReasoner_ExecuteAsync__NoResponse]", noResponseMsg);

            return InvokeResult<AgentExecuteResponse>.FromError(noResponseMsg);
        }

        private sealed class ResponsesToolOutput
        {
            [JsonProperty("tool_call_id")]
            public string ToolCallId { get; set; }

            [JsonProperty("output")]
            public string Output { get; set; }
        }
    }

    
}
