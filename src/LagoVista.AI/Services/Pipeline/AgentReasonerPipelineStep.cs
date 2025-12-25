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

        public AgentReasonerPipelineStep(ILLMClient llmClient, IAgentToolExecutor toolExecutor, IAdminLogger logger, IAgentStreamingContext agentStreamingContext)
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
            
            for (var iteration = 0; iteration < MaxReasoningIterations; iteration++)
            {
                if (ctx.CancellationToken.IsCancellationRequested) { return InvokeResult<AgentPipelineContext>.Abort(); }

                _logger.Trace("[AgentReasoner_ExecuteAsync] Iteration " + (iteration + 1) + " starting. " + "sessionId=" + ctx.SessionId + ", mode=" + ctx.Session.Mode);

                var llmResult = await _llmClient.ExecuteAsync(ctx);
                if (!llmResult.Successful) { return llmResult; }

                if (llmResult.Result.Response.Kind == AgentExecuteResponseKind.Final)
                    return llmResult;

                var pendingClientCalls = new List<ClientToolCall>();

                foreach (var toolCall in ctx.ToolCalls)
                {
                    await _agentStreamingContext.AddWorkflowAsync("calling tool " + toolCall.Name + "...", ctx.CancellationToken);
                    var sw = Stopwatch.StartNew();

                    var callResponse = await _toolExecutor.ExecuteServerToolAsync(toolCall, ctx);

                    await _agentStreamingContext.AddWorkflowAsync((callResponse.Successful ? "success" : "failed") + " calling tool " + toolCall.Name + (callResponse.Successful ? "" : ", err: " + callResponse.ErrorMessage) + " in " + sw.Elapsed.TotalMilliseconds.ToString("0.0") + "ms...", ctx.CancellationToken);

                    if (ctx.CancellationToken.IsCancellationRequested) { return InvokeResult<AgentPipelineContext>.Abort(); }
                    if (!callResponse.Successful) { return InvokeResult<AgentPipelineContext>.FromInvokeResult(callResponse.ToInvokeResult()); }

                    var result = callResponse.Result;

                    if (result.RequiresClientExecution) { pendingClientCalls.Add(new ClientToolCall() {Name = toolCall.Name, ToolCallId = toolCall.ToolCallId, ArgumentsJson = toolCall.ArgumentsJson }); }
                }

                if (pendingClientCalls.Count > 0)
                {
                    ctx.Response.ToolCalls.AddRange(pendingClientCalls);
                    return InvokeResult<AgentPipelineContext>.Create(ctx);
                }
            }

            return InvokeResult<AgentPipelineContext>.FromError("Maximum reasoning iterations exceeded.", "AGENT_REASONER_MAX_ITERATIONS_EXCEEDED");
        }

      
    }
}
