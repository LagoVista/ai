using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Interfaces.Services;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Pipeline;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services
{
    public sealed class AgentReasonerPipelineStep : PipelineStep, IAgentReasonerPipelineStep
    {
        private readonly ILLMClient _llmClient;
        private readonly IAgentToolExecutor _toolExecutor;
        private readonly IAdminLogger _logger;
        private readonly IAgentStreamingContext _agentStreamingContext;
        private readonly IPromptKnowledgeProvider _pkpService;
        private readonly IAgentSessionFactory _sessionFactory;
        private const int MaxReasoningIterations = 8;

        public AgentReasonerPipelineStep(ILLMClient llmClient, IAgentToolExecutor toolExecutor,
            IAgentPipelineContextValidator validator, IPromptKnowledgeProvider pkpService, 
            IAdminLogger logger, IAgentStreamingContext agentStreamingContext, IAgentSessionFactory sessionFactory) : base(validator, logger)
        {
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pkpService = pkpService ?? throw new ArgumentNullException(nameof(pkpService));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        }

        protected override PipelineSteps StepType => PipelineSteps.Reasoner;

        protected override async Task<InvokeResult<IAgentPipelineContext>> ExecuteStepAsync(IAgentPipelineContext ctx)
        {
            for (var iteration = 0; iteration < MaxReasoningIterations; iteration++)
            {
                if (ctx.CancellationToken.IsCancellationRequested) { return InvokeResult<IAgentPipelineContext>.Abort(); }

                _logger.Trace($"{this.Tag()} Iteration " + (iteration + 1) + " starting. " + "sessionId=" + ctx.Session.Id + ", mode=" + ctx.Session.Mode);

                var llmResult = await _llmClient.ExecuteAsync(ctx);
                if (!llmResult.Successful) { return llmResult; }

                // After the call, does the model want to call anything?  if not we are done.
                if (!llmResult.Result.HasPendingToolCalls)
                {
                    if (ctx.ThisTurn.Type.Value == AgentSessionTurnType.ChapterEnd)
                    {
                        ctx.ThisTurn.Type = EntityHeader<AgentSessionTurnType>.Create(AgentSessionTurnType.ChapterStart);
                        var chapterStartTurn = _sessionFactory.CreateTurnForNewChapter(ctx, ctx.Session);
                        ctx.Session.Turns.Add(chapterStartTurn);
                        ctx.AttachNewChapterTurn(chapterStartTurn);
                    }

                    return llmResult;
                }

                ctx = llmResult.Result;

                var originalMode = ctx.Session.AgentMode.Id;

                var newInstructions = new StringBuilder();
                foreach (var toolCall in ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCalls)
                {
                    var sw = Stopwatch.StartNew();

                    var callResponse = await _toolExecutor.ExecuteServerToolAsync(toolCall, ctx);

                    foreach (var warning in callResponse.Warnings)
                    {
                        newInstructions.AppendLine(warning.Message);
                    }   

                    await _agentStreamingContext.AddWorkflowAsync((callResponse.Successful ? "success" : "failed") + " calling tool " + toolCall.Name + (callResponse.Successful ? "" : ", err: " + callResponse.ErrorMessage) + " in " + sw.Elapsed.TotalMilliseconds.ToString("0.0") + "ms...", ctx.CancellationToken);

                    if (ctx.CancellationToken.IsCancellationRequested) { return InvokeResult<IAgentPipelineContext>.Abort(); }
                    if (!callResponse.Successful) { return InvokeResult<IAgentPipelineContext>.FromInvokeResult(callResponse.ToInvokeResult()); }
                    var result = callResponse.Result;
                    ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCallResults.Add(result);
                }

                _logger.Trace($"[JSON.PromptKnowledgeProvider.ToolCallManifest]={JsonConvert.SerializeObject(ctx.PromptKnowledgeProvider.ToolCallManifest)}");

                // After processing all our tool calls, if we still have client tool calls, we need to exit to let the client handle them.
                // upon return we will pickup where we left off.  
                if (llmResult.Result.HasClientToolCalls)
                {
                    _logger.Trace($"{this.Tag()} - Has Client Tools, Exiting Reasoner Step to let Client Execute Tools.");
                    return InvokeResult<IAgentPipelineContext>.Create(ctx);
                }

                if (originalMode != ctx.Session.AgentMode.Id)
                {
                    newInstructions.AppendLine($"- Agent Changed Mode from {originalMode} to {ctx.Session.AgentMode.Text}");
                    _logger.Trace($"{this.Tag()} - Mode Change Detected Populate PKP {originalMode} -> {ctx.Session.AgentMode.Text}");
                   var pkpResult = await _pkpService.PopulateAsync(ctx, true);
                   if(!pkpResult.Successful) return InvokeResult<IAgentPipelineContext>.FromInvokeResult(pkpResult.ToInvokeResult());
                }

                ctx.SetInstructions(newInstructions.ToString());
            }

            return InvokeResult<IAgentPipelineContext>.FromError("Maximum reasoning iterations exceeded.", "AGENT_REASONER_MAX_ITERATIONS_EXCEEDED");
        }
    }
}
