using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Pipeline;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services
{
    public sealed class AgentReasonerPipelineStep : PipelineStep, IAgentReasonerPipelineStep
    {
        private readonly ILLMClient _llmClient;
        private readonly IAgentToolExecutor _toolExecutor;
        private readonly IAdminLogger _logger;
        private readonly IAgentStreamingContext _agentStreamingContext;
     
        private const int MaxReasoningIterations = 4;

        public AgentReasonerPipelineStep(ILLMClient llmClient, IAgentToolExecutor toolExecutor,
            IAgentPipelineContextValidator validator,
            IAdminLogger logger, IAgentStreamingContext agentStreamingContext) : base(validator, logger)
        {
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
        }

        protected override PipelineSteps StepType => PipelineSteps.Reasoner;

        protected override async Task<InvokeResult<IAgentPipelineContext>> ExecuteStepAsync(IAgentPipelineContext ctx)
        {
            for (var iteration = 0; iteration < MaxReasoningIterations; iteration++)
            {
                if (ctx.CancellationToken.IsCancellationRequested) { return InvokeResult<IAgentPipelineContext>.Abort(); }

                _logger.Trace("[AgentReasoner_ExecuteAsync] Iteration " + (iteration + 1) + " starting. " + "sessionId=" + ctx.Session.Id + ", mode=" + ctx.Session.Mode);

                var llmResult = await _llmClient.ExecuteAsync(ctx);
                if (!llmResult.Successful) { return llmResult; }

                if (!llmResult.Result.HasPendingToolCalls)
                    return llmResult;

                ctx = llmResult.Result;

                foreach (var toolCall in ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCalls)
                {
                    await _agentStreamingContext.AddWorkflowAsync("calling tool " + toolCall.Name + "...", ctx.CancellationToken);
                    var sw = Stopwatch.StartNew();

                    var callResponse = await _toolExecutor.ExecuteServerToolAsync(toolCall, ctx);

                    await _agentStreamingContext.AddWorkflowAsync((callResponse.Successful ? "success" : "failed") + " calling tool " + toolCall.Name + (callResponse.Successful ? "" : ", err: " + callResponse.ErrorMessage) + " in " + sw.Elapsed.TotalMilliseconds.ToString("0.0") + "ms...", ctx.CancellationToken);

                    if (ctx.CancellationToken.IsCancellationRequested) { return InvokeResult<IAgentPipelineContext>.Abort(); }
                    if (!callResponse.Successful) { return InvokeResult<IAgentPipelineContext>.FromInvokeResult(callResponse.ToInvokeResult()); }

                    var result = callResponse.Result;
                    ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCallResults.Add(result);
                }

                // After processing all our tool calls, if we still have client tool calls, we need to exit to let the client handle them.
                // upon return we will pickup where we left off.  
                if (llmResult.Result.HasClientToolCalls)
                {
                    return InvokeResult<IAgentPipelineContext>.Create(ctx);
                }
            }

            return InvokeResult<IAgentPipelineContext>.FromError("Maximum reasoning iterations exceeded.", "AGENT_REASONER_MAX_ITERATIONS_EXCEEDED");
        }
    }
}
