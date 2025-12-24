using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
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
    ///
    /// Contract notes (AGN-031):
    /// - The reasoner owns correctness and exit modes.
    /// - The reasoner may read provider state, but must not mutate providers except clearing turn-scoped registers after they have been emitted by prompt composition.
    /// - Tool execution is sequential; first failure terminates.
    /// - Client tool continuation (Exit Mode 2) returns pending client calls for out-of-band execution.
    /// </summary>
    public sealed class AgentReasonerPipelineStep : IAgentReasonerPipelineStep
    {
        private readonly ILLMClient _llmClient;
        private readonly IAgentToolExecutor _toolExecutor;
        private readonly IAdminLogger _logger;
        private readonly IAgentStreamingContext _agentStreamingContext;
     
        private const int MaxReasoningIterations = 4;

        public AgentReasonerPipelineStep(ILLMClient llmClient, IAgentToolExecutor toolExecutor, IAdminLogger logger, IAgentStreamingContext agentStreamingContext, IModeEntryBootstrapService modeEntryBootstrapService)
        {
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx)
        {
            var validation = ValidateInputs(ctx);
            if (!validation.Successful) { return validation; }
          
            
            var llmResult = await _llmClient.ExecuteAsync(ctx);
            if (!llmResult.Successful) { return llmResult; }

            if (llmResult.Result.Response.Kind == AgentExecuteResponse.ResponseKindOk)
                return llmResult;

            for (var iteration = 0; iteration < MaxReasoningIterations; iteration++)
            {
                if (ctx.CancellationToken.IsCancellationRequested) { return InvokeResult<AgentPipelineContext>.Abort(); }

                _logger.Trace("[AgentReasoner_ExecuteAsync] Iteration " + (iteration + 1) + " starting. " + "sessionId=" + ctx.SessionId + ", mode=" + ctx.Request.Mode);

                llmResult = await _llmClient.ExecuteAsync(ctx);
                if (!llmResult.Successful) { return llmResult; }

                if (llmResult.Result.Response.Kind == AgentExecuteResponse.ResponseKindOk)
                    return llmResult;

                var executedServerCalls = new List<AgentToolCall>();
                var pendingClientCalls = new List<AgentToolCall>();

                var execResult = await ExecuteToolBatchAsync(ctx, executedServerCalls, pendingClientCalls, ctx.CancellationToken);
                if (!execResult.Successful) { return execResult; }

                var toolOutputs = executedServerCalls.Where(c => c.WasExecuted && !string.IsNullOrWhiteSpace(c.CallId))
                    .Select(c => new ResponsesToolOutput { 
                        ToolCallId = c.CallId, 
                        Output = string.IsNullOrWhiteSpace(c.ResultJson) ? "{}" : c.ResultJson }
                    ).ToList();

                ctx.Request.ToolResultsJson = JsonConvert.SerializeObject(toolOutputs);

                if (pendingClientCalls.Count > 0)
                {
                    MergeCallsForClientToolContinuation(ctx.Response, executedServerCalls, pendingClientCalls);
                    return InvokeResult<AgentPipelineContext>.Create(ctx);
                }

                if (executedServerCalls.Count == 0)
                {
                    return InvokeResult<AgentPipelineContext>.Create(ctx);
                }

                try
                {
                    ctx.Request.ToolResults = executedServerCalls;
                    ctx.Request.ToolResultsJson = JsonConvert.SerializeObject(executedServerCalls);
                }
                catch (Exception ex)
                {
                    _logger.AddException("[AgentReasoner_ExecuteAsync__ToolResultsSerializeException]", ex);
                    return InvokeResult<AgentPipelineContext>.FromError("Failed to serialize tool results for LLM follow-up call: " + ex.Message, "AGENT_REASONER_TOOL_RESULTS_SERIALIZE_FAILED");
                }

                ctx.Request.ResponseContinuationId = null;
            }

            return InvokeResult<AgentPipelineContext>.FromError("Maximum reasoning iterations exceeded.", "AGENT_REASONER_MAX_ITERATIONS_EXCEEDED");
        }

        private InvokeResult<AgentPipelineContext> ValidateInputs(AgentPipelineContext ctx)
        {
            if (ctx == null) { return InvokeResult<AgentPipelineContext>.FromError("AgentPipelineContext cannot be null.", "AGENT_REASONER_NULL_CONTEXT"); }
            if (ctx.AgentContext == null) { return InvokeResult<AgentPipelineContext>.FromError("AgentContext is required.", "AGENT_REASONER_MISSING_AGENT_CONTEXT"); }
            if (ctx.ConversationContext == null) { return InvokeResult<AgentPipelineContext>.FromError("ConversationContext is required.", "AGENT_REASONER_MISSING_CONVERSATION_CONTEXT"); }
            if (ctx.Request == null) { return InvokeResult<AgentPipelineContext>.FromError("AgentExecuteRequest is required.", "AGENT_REASONER_MISSING_REQUEST"); }
            if (ctx.Org == null || EntityHeader.IsNullOrEmpty(ctx.Org)) { return InvokeResult<AgentPipelineContext>.FromError("Org is required.", "AGENT_REASONER_MISSING_ORG"); }
            if (ctx.User == null || EntityHeader.IsNullOrEmpty(ctx.User)) { return InvokeResult<AgentPipelineContext>.FromError("User is required.", "AGENT_REASONER_MISSING_USER"); }
            if (string.IsNullOrWhiteSpace(ctx.SessionId)) { return InvokeResult<AgentPipelineContext>.FromError("ConversationId (session id) is required.", "AGENT_REASONER_MISSING_CONVERSATION_ID"); }

            var turnId = ResolveCurrentTurnId(ctx);
            if (string.IsNullOrWhiteSpace(turnId)) { return InvokeResult<AgentPipelineContext>.FromError("CurrentTurnId is required.", "AGENT_REASONER_MISSING_CURRENT_TURN_ID"); }

            return InvokeResult<AgentPipelineContext>.Create(ctx);
        }

        private string ResolveCurrentTurnId(AgentPipelineContext ctx)
        {
            var currentTurnId = string.Empty;
            try { currentTurnId = ctx.Request?.CurrentTurnId; } catch { }

            if (string.IsNullOrWhiteSpace(currentTurnId) && ctx.Turn != null) { currentTurnId = ctx.Turn.Id; }
            return currentTurnId;
        }

        private async Task<InvokeResult<AgentPipelineContext>> ExecuteToolBatchAsync(AgentPipelineContext ctx, List<AgentToolCall> executedServerCalls, List<AgentToolCall> pendingClientCalls, CancellationToken cancellationToken)
        {
            foreach (var toolCall in ctx.Response.ToolCalls)
            {
                await _agentStreamingContext.AddWorkflowAsync("calling tool " + toolCall.Name + "...", cancellationToken);
                var sw = Stopwatch.StartNew();

                var callResponse = await _toolExecutor.ExecuteServerToolAsync(toolCall, ctx);

                await _agentStreamingContext.AddWorkflowAsync((callResponse.Successful ? "success" : "failed") + " calling tool " + toolCall.Name + (callResponse.Successful ? "" : ", err: " + callResponse.ErrorMessage) + " in " + sw.Elapsed.TotalMilliseconds.ToString("0.0") + "ms...", cancellationToken);

                if (cancellationToken.IsCancellationRequested) { return InvokeResult<AgentPipelineContext>.Abort(); }
                if (!callResponse.Successful) { return InvokeResult<AgentPipelineContext>.FromInvokeResult(callResponse.ToInvokeResult()); }

                var updatedCall = callResponse.Result;
                if (updatedCall == null) { continue; }
           
                if (updatedCall.WasExecuted && !updatedCall.RequiresClientExecution) { executedServerCalls.Add(updatedCall); }
                else if (updatedCall.WasExecuted && updatedCall.RequiresClientExecution) { pendingClientCalls.Add(updatedCall); }
                else { executedServerCalls.Add(updatedCall); }
            }

            return InvokeResult<AgentPipelineContext>.Create(ctx);
        }

        private void MergeCallsForClientToolContinuation(AgentExecuteResponse response, List<AgentToolCall> executedServerCalls, List<AgentToolCall> pendingClientCalls)
        {
            var mergedCalls = new List<AgentToolCall>();
            mergedCalls.AddRange(executedServerCalls);
            mergedCalls.AddRange(pendingClientCalls);
            response.ToolCalls = mergedCalls;
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
