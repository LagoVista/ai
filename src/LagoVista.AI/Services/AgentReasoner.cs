using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// Pipeline step that performs the LLM/tool loop and sets ctx.Response.
    /// </summary>
    public class AgentReasoner : IAgentReasoner
    {
        private readonly ILLMClient _llmClient;
        private readonly IAgentToolExecutor _toolExecutor;
        private readonly IAdminLogger _logger;
        private readonly IAgentStreamingContext _agentStreamingContext;
        private readonly IModeEntryBootstrapService _modeEntryBootstrapService;

        private const int MaxReasoningIterations = 4;

        public AgentReasoner(
            ILLMClient llmClient,
            IAgentToolExecutor toolExecutor,
            IAdminLogger logger,
            IAgentStreamingContext agentStreamingContext,
            IModeEntryBootstrapService modeEntryBootstrapService)
        {
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
            _modeEntryBootstrapService = modeEntryBootstrapService ?? throw new ArgumentNullException(nameof(modeEntryBootstrapService));
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx)
        {
            if (ctx == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "AgentPipelineContext cannot be null.",
                    "AGENT_REASONER_NULL_CONTEXT");
            }

            if (ctx.AgentContext == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "AgentContext is required.",
                    "AGENT_REASONER_MISSING_AGENT_CONTEXT");
            }

            if (ctx.ConversationContext == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "ConversationContext is required.",
                    "AGENT_REASONER_MISSING_CONVERSATION_CONTEXT");
            }

            if (ctx.Request == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "AgentExecuteRequest is required.",
                    "AGENT_REASONER_MISSING_REQUEST");
            }

            if (ctx.Org == null || EntityHeader.IsNullOrEmpty(ctx.Org))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "Org is required.",
                    "AGENT_REASONER_MISSING_ORG");
            }

            if (ctx.User == null || EntityHeader.IsNullOrEmpty(ctx.User))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "User is required.",
                    "AGENT_REASONER_MISSING_USER");
            }

            var sessionId = !string.IsNullOrWhiteSpace(ctx.SessionId)
                ? ctx.SessionId
                : ctx.Request.SessionId;

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "SessionId (session id) is required.",
                    "AGENT_REASONER_MISSING_CONVERSATION_ID");
            }

            var currentTurnId = string.Empty;
            try { currentTurnId = ctx.Request.CurrentTurnId; } catch { }

            if (string.IsNullOrWhiteSpace(currentTurnId) && ctx.Turn != null)
            {
                currentTurnId = ctx.Turn.Id;
            }

            if (string.IsNullOrWhiteSpace(currentTurnId))
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "CurrentTurnId is required.",
                    "AGENT_REASONER_MISSING_CURRENT_TURN_ID");
            }

            AgentExecuteResponse lastResponse = null;
            string pendingWelcomeMessage = null;

            var agentContext = ctx.AgentContext;
            var conversationContext = ctx.ConversationContext;
            var request = ctx.Request;
            var ragContextBlock = ctx.RagContextBlock ?? string.Empty;

            for (var iteration = 0; iteration < MaxReasoningIterations; iteration++)
            {
                _logger.Trace(
                    "[AgentReasoner_ExecuteAsync] Iteration " + (iteration + 1) + " starting. " +
                    "sessionId=" + sessionId + ", mode=" + request.Mode + ", SessionId=" + request.SessionId);

                if (ctx.CancellationToken.IsCancellationRequested)
                {
                    return InvokeResult<AgentPipelineContext>.Abort();
                }

                // Ensure the LLM step has everything it needs on ctx (future-proofing).
                ctx.AgentContext = agentContext;
                ctx.ConversationContext = conversationContext;
                ctx.Request = request;
                ctx.RagContextBlock = ragContextBlock;

                // IMPORTANT: LLM step must set ctx.Response
                var llmCtxResult = await _llmClient.ExecuteAsync(ctx);
                if (!llmCtxResult.Successful)
                {
                    _logger.AddError(
                        "[AgentReasoner_ExecuteAsync__LLMFailed]",
                        "LLM call failed on iteration " + (iteration + 1) + ": " + llmCtxResult.ErrorMessage);

                    return InvokeResult<AgentPipelineContext>.FromInvokeResult(llmCtxResult.ToInvokeResult());
                }

                var updatedCtx = llmCtxResult.Result;
                if (updatedCtx == null)
                {
                    const string nullMsg = "LLM step returned null AgentPipelineContext.";
                    _logger.AddError("[AgentReasoner_ExecuteAsync__NullPipelineContext]", nullMsg);
                    return InvokeResult<AgentPipelineContext>.FromError(nullMsg, "AGENT_REASONER_NULL_PIPELINE_CONTEXT");
                }

                lastResponse = updatedCtx.Response;
                if (lastResponse == null)
                {
                    const string nullMsg = "LLM step did not set ctx.Response.";
                    _logger.AddError("[AgentReasoner_ExecuteAsync__NullResponse]", nullMsg);
                    return InvokeResult<AgentPipelineContext>.FromError(nullMsg, "AGENT_REASONER_NULL_RESPONSE");
                }

                // If there are no tool calls, we are done.
                if (lastResponse.ToolCalls == null || lastResponse.ToolCalls.Count == 0)
                {
                    _logger.Trace("[AgentReasoner_ExecuteAsync] No tool calls detected. Returning final LLM response.");

                    if (string.IsNullOrWhiteSpace(lastResponse.Mode) && !string.IsNullOrWhiteSpace(request.Mode))
                    {
                        lastResponse.Mode = request.Mode;
                    }

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

                    updatedCtx.Response = lastResponse;
                    return InvokeResult<AgentPipelineContext>.Create(updatedCtx);
                }

                _logger.Trace(
                    "[AgentReasoner_ExecuteAsync] Detected " + lastResponse.ToolCalls.Count + " tool call(s) " +
                    "from LLM on iteration " + (iteration + 1) + ".");

                var toolContext = new AgentToolExecutionContext
                {
                    AgentContext = agentContext,
                    ConversationContext = conversationContext,
                    Request = request,
                    SessionId = sessionId,
                    CurrentTurnId = currentTurnId,
                    Org = updatedCtx.Org,
                    User = updatedCtx.User
                };

                var executedServerCalls = new List<AgentToolCall>();
                var pendingClientCalls = new List<AgentToolCall>();

                foreach (var toolCall in lastResponse.ToolCalls)
                {
                    await _agentStreamingContext.AddWorkflowAsync("calling tool " + toolCall.Name + "...", ctx.CancellationToken);
                    var sw = Stopwatch.StartNew();

                    var updatedCallResponse = await _toolExecutor.ExecuteServerToolAsync(toolCall, ctx);

                    if (updatedCallResponse.Successful)
                    {
                        await _agentStreamingContext.AddWorkflowAsync(
                            "success calling tool " + toolCall.Name + " in " + sw.Elapsed.TotalMilliseconds.ToString("0.0") + "ms...",
                            ctx.CancellationToken);
                    }
                    else
                    {
                        await _agentStreamingContext.AddWorkflowAsync(
                            "failed to call tool " + toolCall.Name + ", err: " + updatedCallResponse.ErrorMessage + " in " + sw.Elapsed.TotalMilliseconds.ToString("0.0") + "ms...",
                            ctx.CancellationToken);
                    }

                    if (ctx.CancellationToken.IsCancellationRequested)
                    {
                        return InvokeResult<AgentPipelineContext>.Abort();
                    }

                    if (!updatedCallResponse.Successful)
                    {
                        return InvokeResult<AgentPipelineContext>.FromInvokeResult(updatedCallResponse.ToInvokeResult());
                    }

                    var updatedCall = updatedCallResponse.Result;


                    if (updatedCall != null && updatedCall.WasExecuted && !updatedCall.RequiresClientExecution)
                    {
                        executedServerCalls.Add(updatedCall);
                    }
                    else if (updatedCall != null && updatedCall.WasExecuted && updatedCall.RequiresClientExecution)
                    {
                        pendingClientCalls.Add(updatedCall);
                    }
                    else
                    {
                        if (updatedCall != null)
                        {
                            executedServerCalls.Add(updatedCall);
                        }
                    }
                }

                // MODE CHANGE HANDLING
                string newModeFromTool = null;
                var modeChangeDetected = false;
                var successfulModeChangeCount = 0;

                foreach (var call in executedServerCalls)
                {
                    if ( !call.WasExecuted ||
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

                            var bootStrapRequest = new ModeEntryBootstrapRequest
                            {
                                ModeKey = newModeFromTool,
                                ToolContext = toolContext
                            };

                            var bootStrapResult = await _modeEntryBootstrapService.ExecuteAsync(bootStrapRequest, ctx.CancellationToken);
                            if (!bootStrapResult.Successful)
                            {
                                return InvokeResult<AgentPipelineContext>.FromInvokeResult(bootStrapResult.ToInvokeResult());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.AddException("[AgentReasoner_ExecuteAsync__ModeChangeResultDeserializeError]", ex);
                    }
                }

                if (successfulModeChangeCount > 1 && !string.IsNullOrWhiteSpace(newModeFromTool))
                {
                    _logger.AddError(
                        "[AgentReasoner_ExecuteAsync__MultipleModeChanges]",
                        "Detected " + successfulModeChangeCount + " successful mode-change tool calls in a single turn. Using last mode '" + newModeFromTool + "'.");
                }

                if (modeChangeDetected && !string.IsNullOrWhiteSpace(newModeFromTool))
                {
                    request.Mode = newModeFromTool;
                    lastResponse.Mode = newModeFromTool;

                    try
                    {
                        var mode = agentContext.AgentModes.Single(m => m.Key == newModeFromTool);
                        var welcome = mode.WelcomeMessage;
                        if (!string.IsNullOrWhiteSpace(welcome))
                        {
                            pendingWelcomeMessage = welcome;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.AddException("[AgentReasoner_ExecuteAsync__WelcomeMessageException]", ex);
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(lastResponse.Mode) && !string.IsNullOrWhiteSpace(request.Mode))
                    {
                        lastResponse.Mode = request.Mode;
                    }
                }

                // Build tool outputs for continuation flow
                var toolOutputs = executedServerCalls
                    .Where(c => c.WasExecuted && !string.IsNullOrWhiteSpace(c.CallId))
                    .Select(c => new ResponsesToolOutput
                    {
                        ToolCallId = c.CallId,
                        Output = string.IsNullOrWhiteSpace(c.ResultJson) ? "{}" : c.ResultJson
                    })
                    .ToList();

                request.ToolResultsJson = JsonConvert.SerializeObject(toolOutputs);

                if (pendingClientCalls.Count > 0)
                {
                    _logger.Trace("[AgentReasoner_ExecuteAsync] Client tools detected. Returning response with mixed server/client tool calls.");

                    var mergedCalls = new List<AgentToolCall>();
                    mergedCalls.AddRange(executedServerCalls);
                    mergedCalls.AddRange(pendingClientCalls);

                    lastResponse.ToolCalls = mergedCalls;

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

                    updatedCtx.Response = lastResponse;
                    return InvokeResult<AgentPipelineContext>.Create(updatedCtx);
                }

                if (executedServerCalls.Count == 0)
                {
                    _logger.Trace("[AgentReasoner_ExecuteAsync] Only server tools requested, but none executed. Returning last response as-is.");

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

                    updatedCtx.Response = lastResponse;
                    return InvokeResult<AgentPipelineContext>.Create(updatedCtx);
                }

                _logger.Trace(
                    "[AgentReasoner_ExecuteAsync] Executed " + executedServerCalls.Count + " server-side tool(s). Preparing to call LLM again with tool results.");

                try
                {
                    request.ToolResults = executedServerCalls;
                    request.ToolResultsJson = JsonConvert.SerializeObject(executedServerCalls);
                }
                catch (Exception ex)
                {
                    _logger.AddException("[AgentReasoner_ExecuteAsync__ToolResultsSerializeException]", ex);
                    var msg = "Failed to serialize tool results for LLM follow-up call: " + ex.Message;
                    return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_REASONER_TOOL_RESULTS_SERIALIZE_FAILED");
                }

                request.ResponseContinuationId = null;

                // Loop back: next iteration will call _llmClient.ExecuteAsync(updatedCtx)
                ctx = updatedCtx;
            }

            if (lastResponse != null)
            {
                const string warning = "Maximum reasoning iterations reached in AgentReasoner.";
                _logger.AddError("[AgentReasoner_ExecuteAsync__MaxIterations]", warning);

                if (lastResponse.Warnings == null)
                {
                    lastResponse.Warnings = new List<string>();
                }

                lastResponse.Warnings.Add(warning);

                if (string.IsNullOrWhiteSpace(lastResponse.Mode) && !string.IsNullOrWhiteSpace(ctx.Request.Mode))
                {
                    lastResponse.Mode = ctx.Request.Mode;
                }

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

                ctx.Response = lastResponse;
                return InvokeResult<AgentPipelineContext>.Create(ctx);
            }

            const string noResponseMsg = "AgentReasoner completed without producing any LLM response.";
            _logger.AddError("[AgentReasoner_ExecuteAsync__NoResponse]", noResponseMsg);
            return InvokeResult<AgentPipelineContext>.FromError(noResponseMsg, "AGENT_REASONER_NO_RESPONSE");
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
