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
using LagoVista.AI.Services.Tools;
using System.Diagnostics;

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
    ///
    /// AGN-011 additionally requires:
    /// - Detecting mode changes via ModeChangeTool (TUL-007).
    /// - Updating request.Mode and response.Mode accordingly.
    /// - Emitting a mode-specific welcome message when the mode changes.
    /// </summary>
    public class AgentReasoner : IAgentReasoner
    {
        private readonly ILLMClient _llmClient;
        private readonly IAgentToolExecutor _toolExecutor;
        private readonly IAdminLogger _logger;
        private readonly IAgentStreamingContext _agentStreamingContext;

        // Safety cap to avoid runaway tool-trigger loops.
        private const int MaxReasoningIterations = 4;

        public AgentReasoner(
            ILLMClient llmClient,
            IAgentToolExecutor toolExecutor,
            IAdminLogger logger,
            IAgentStreamingContext agentStreamingContext)
        {
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
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

            // Accumulate the "mode welcome" across iterations.
            // If multiple mode changes occur, the *last* one wins.
            string pendingWelcomeMessage = null;

            for (var iteration = 0; iteration < MaxReasoningIterations; iteration++)
            {
                _logger.Trace(
                    $"[AgentReasoner_ExecuteAsync] Iteration {iteration + 1} starting. " +
                    $"sessionId={sessionId}, mode={request.Mode}, conversationId={request.ConversationId}");

                if(cancellationToken.IsCancellationRequested)
                {
                    return InvokeResult<AgentExecuteResponse>.Abort();
                }

                var llmResult = await _llmClient.GetAnswerAsync(
                    agentContext,
                    conversationContext,
                    request,
                    ragContextBlock,
                    sessionId,
                    cancellationToken);

                if (!llmResult.Successful)
                {
                    _logger.AddError(
                        "[AgentReasoner_ExecuteAsync__LLMFailed]",
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

                    // If LLM didn't set Mode, default to request.Mode.
                    if (string.IsNullOrWhiteSpace(lastResponse.Mode) && !string.IsNullOrWhiteSpace(request.Mode))
                    {
                        lastResponse.Mode = request.Mode;
                    }

                    // If a mode change happened in a prior iteration, prepend the welcome now.
                    if (!string.IsNullOrWhiteSpace(pendingWelcomeMessage))
                    {
                        if (string.IsNullOrWhiteSpace(lastResponse.Text))
                        {
                            lastResponse.Text = pendingWelcomeMessage;
                        }
                        else
                        {
                            lastResponse.Text = pendingWelcomeMessage + "\n\n" + lastResponse.Text;
                        }

                        // Make sure the InvokeResult we return sees the updated response.
                        llmResult.Result = lastResponse;
                    }

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
                    await _agentStreamingContext.AddWorkflowAsync($"...calling tool {toolCall.Name}...",cancellationToken);
                    var sw = Stopwatch.StartNew();

                    // Let the executor decide if this is a server tool or not.
                    var updatedCallResponse = await _toolExecutor.ExecuteServerToolAsync(
                        toolCall,
                        toolContext,
                        cancellationToken);

                    if(updatedCallResponse.Successful)
                        await _agentStreamingContext.AddWorkflowAsync($"...success calling tool {toolCall.Name} in {sw.Elapsed.TotalMilliseconds.ToString("0.0")}ms...", cancellationToken);
                    else
                        await _agentStreamingContext.AddWorkflowAsync($"...failed to call tool {toolCall.Name}, err: {updatedCallResponse.ErrorMessage} in {sw.Elapsed.TotalMilliseconds.ToString("0.0")}ms...", cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return InvokeResult<AgentExecuteResponse>.Abort();
                    }

                    if (!updatedCallResponse.Successful)
                    {
                        return InvokeResult<AgentExecuteResponse>.FromInvokeResult(
                            updatedCallResponse.ToInvokeResult());
                    }



                    var updatedCall = updatedCallResponse.Result;

                    // All server executions should mark IsServerTool = true. If not, log.
                    if (!updatedCall.IsServerTool)
                    {
                        _logger.AddError(
                            "[AgentReasoner_ExecuteAsync__UnexpectedNonServerToolCall]",
                            $"Tool '{updatedCall?.Name ?? "<null>"}' returned IsServerTool=false during server execution.");
                    }

                    if (updatedCall.WasExecuted && !updatedCall.RequiresClientExecution)
                    {
                        // Either:
                        // - Server-final tool (IsToolFullyExecutedOnServer == true), OR
                        // - Preflight failed/short-circuited, and we do NOT want a client retry.
                        executedServerCalls.Add(updatedCall);
                    }
                    else if (updatedCall.WasExecuted && updatedCall.RequiresClientExecution)
                    {
                        // Server preflight succeeded, but final behavior needs client execution.
                        pendingClientCalls.Add(updatedCall);
                    }
                    else
                    {
                        // WasExecuted == false -> error/cancellation prior to running logic.
                        // Surface to caller for visibility, but do not send to client.
                        executedServerCalls.Add(updatedCall);
                    }
                }

                //
                // MODE CHANGE HANDLING (TUL-007 / AGN-011)
                //
                string newModeFromTool = null;
                bool modeChangeDetected = false;
                int successfulModeChangeCount = 0;

                foreach (var call in executedServerCalls)
                {
                    if (!call.IsServerTool ||
                        !call.WasExecuted ||
                        !string.Equals(call.Name, ModeChangeTool.ToolName, StringComparison.OrdinalIgnoreCase) ||
                        string.IsNullOrWhiteSpace(call.ResultJson))
                    {
                        continue;
                    }

                    try
                    {
                        var modeResult = JsonConvert.DeserializeObject<ModeChangeResult>(call.ResultJson);
                        if (modeResult == null)
                        {
                            continue;
                        }

                        if (modeResult.Success && !string.IsNullOrWhiteSpace(modeResult.Mode))
                        {
                            successfulModeChangeCount++;
                            modeChangeDetected = true;
                            newModeFromTool = modeResult.Mode;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.AddException(
                            "[AgentReasoner_ExecuteAsync__ModeChangeResultDeserializeError]",
                            ex);
                    }
                }

                if (successfulModeChangeCount > 1 && !string.IsNullOrWhiteSpace(newModeFromTool))
                {
                    _logger.AddError(
                        "[AgentReasoner_ExecuteAsync__MultipleModeChanges]",
                        $"Detected {successfulModeChangeCount} successful mode-change " +
                        $"tool calls in a single turn. Using last mode '{newModeFromTool}'.");
                }

                if (modeChangeDetected && !string.IsNullOrWhiteSpace(newModeFromTool))
                {
                    // Update the in-flight request mode so any subsequent LLM
                    // calls in this session use the new mode.
                    request.Mode = newModeFromTool;

                    // Ensure the current response also reflects the new mode.
                    lastResponse.Mode = newModeFromTool;

                    // Fetch and store the mode-specific welcome message.
                    try
                    {
                        var mode = agentContext.AgentModes.Single(m => m.Key == newModeFromTool);

                        var welcome = mode.WelcomeMessage;
                        if (!string.IsNullOrWhiteSpace(welcome))
                        {
                            // Last welcome wins if multiple changes occur.
                            pendingWelcomeMessage = welcome;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.AddException(
                            "[AgentReasoner_ExecuteAsync__WelcomeMessageException]",
                            ex);
                    }
                }
                else
                {
                    // If no mode change occurred in this iteration, make sure response mode at
                    // least reflects the current request mode for this turn.
                    if (string.IsNullOrWhiteSpace(lastResponse.Mode) && !string.IsNullOrWhiteSpace(request.Mode))
                    {
                        lastResponse.Mode = request.Mode;
                    }
                }

                //
                // Build tool outputs for the Responses API tool-result continuation flow.
                //
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

                    // If we have a welcome pending, prepend it to the text before returning.
                    if (!string.IsNullOrWhiteSpace(pendingWelcomeMessage))
                    {
                        if (string.IsNullOrWhiteSpace(lastResponse.Text))
                        {
                            lastResponse.Text = pendingWelcomeMessage;
                        }
                        else
                        {
                            lastResponse.Text = pendingWelcomeMessage + "\n\n" + lastResponse.Text;
                        }
                    }

                    return InvokeResult<AgentExecuteResponse>.Create(lastResponse);
                }

                // Only server tools were requested. If none executed successfully,
                // there's nothing more we can do; return the last response as-is.
                if (executedServerCalls.Count == 0)
                {
                    _logger.Trace(
                        "[AgentReasoner_ExecuteAsync] Only server tools requested, but none executed. " +
                        "Returning last response as-is.");

                    // Apply pending welcome if we have one.
                    if (!string.IsNullOrWhiteSpace(pendingWelcomeMessage))
                    {
                        if (string.IsNullOrWhiteSpace(lastResponse.Text))
                        {
                            lastResponse.Text = pendingWelcomeMessage;
                        }
                        else
                        {
                            lastResponse.Text = pendingWelcomeMessage + "\n\n" + lastResponse.Text;
                        }
                    }

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

                // IMPORTANT:
                // We do NOT propagate the previous response id when feeding back tool results
                // as plain text. Doing so would cause the Responses API to expect structured
                // tool outputs for the earlier function_call ids.
                request.ResponseContinuationId = null;

                // Loop back to call the LLM again, now with tool results (and possibly updated mode).
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

                // Ensure final response mode reflects the effective request mode.
                if (string.IsNullOrWhiteSpace(lastResponse.Mode) && !string.IsNullOrWhiteSpace(request.Mode))
                {
                    lastResponse.Mode = request.Mode;
                }

                // Apply pending welcome, if any.
                if (!string.IsNullOrWhiteSpace(pendingWelcomeMessage))
                {
                    if (string.IsNullOrWhiteSpace(lastResponse.Text))
                    {
                        lastResponse.Text = pendingWelcomeMessage;
                    }
                    else
                    {
                        lastResponse.Text = pendingWelcomeMessage + "\n\n" + lastResponse.Text;
                    }
                }

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

        private sealed class ModeChangeResult
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("mode")]
            public string Mode { get; set; }

            [JsonProperty("branch")]
            public bool Branch { get; set; }

            [JsonProperty("reason")]
            public string Reason { get; set; }
        }
    }
}
