using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Interfaces.Services;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Pipeline;
using LagoVista.AI.Services.Tools;
using LagoVista.Core;
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
        private readonly IAgentSessionTurnChapterStore _archiveStore;
        private readonly IAgentToolLoopGuard _toolLoopGuard;
        private const int MaxReasoningIterations = 8;

        public AgentReasonerPipelineStep(ILLMClient llmClient, IAgentToolExecutor toolExecutor,
            IAgentPipelineContextValidator validator, IPromptKnowledgeProvider pkpService, IAgentToolLoopGuard reasoningGuard,
            IAdminLogger logger, IAgentStreamingContext agentStreamingContext, IAgentSessionTurnChapterStore archiveStore, IAgentSessionFactory sessionFactory) : base(validator, logger)
        {
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _archiveStore = archiveStore ?? throw new ArgumentNullException(nameof(archiveStore));
            _pkpService = pkpService ?? throw new ArgumentNullException(nameof(pkpService));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
            _toolLoopGuard = reasoningGuard ?? throw new ArgumentNullException(nameof(reasoningGuard));    
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
                        var currentChapter = ctx.Session.Chapters?.FirstOrDefault(c => c.Id == ctx.Session.CurrentChapter.Id);
                        if (currentChapter == null)
                            return InvokeResult<IAgentPipelineContext>.FromError("current chapter not found, potentially legacy session and not supported.");

                        // Archive turns.
                        var archive = await _archiveStore.SaveAsync(ctx.Session, currentChapter, ctx.Session.Turns, ctx.Envelope.User, ctx.CancellationToken);
                        var newChapter = _sessionFactory.CreateBoundaryTurnForNewChapter(ctx);
                        ctx.Session.Chapters.Add(newChapter);
                        var chapterStartTurn = _sessionFactory.CreateTurnForNewChapter(ctx);
                        ctx.AttachNewChapterTurn(chapterStartTurn);
                        ctx.Session.Turns.Clear();
                        ctx.Session.Turns.Add(chapterStartTurn);
                        ctx.ResponsePayload.Usage = new Core.AI.Models.LlmUsage();
                    }

                    return llmResult;
                }

                ctx = llmResult.Result;

                var originalMode = ctx.Session.AgentMode.Id;

                var newInstructions = new StringBuilder();

                var hasAnyToolResultsThisTurn = ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCallResults.Any();
                foreach (var toolCall in ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCalls)
                {
                    var sw = Stopwatch.StartNew();

                    var decision = _toolLoopGuard.Evaluate(toolCall, iteration, MaxReasoningIterations, hasAnyToolResultsThisTurn);

                    if (!String.IsNullOrWhiteSpace(decision.AdditionalInstructions))
                    {
                        newInstructions.AppendLine(decision.AdditionalInstructions);
                    }

                    if (decision.Action == ToolLoopAction.SuppressWithSyntheticResult)
                    {
                        // IMPORTANT: still add a tool result to satisfy protocol
                        ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCallResults.Add(decision.SyntheticResult);

                        await _agentStreamingContext.AddWorkflowAsync(
                            "success calling tool " + toolCall.Name + " (suppressed: loop detected)",
                            ctx.CancellationToken);

                        continue;
                    }

                    var callResponse = await _toolExecutor.ExecuteServerToolAsync(toolCall, ctx);

                    foreach (var warning in callResponse.Warnings)
                    {
                        newInstructions.AppendLine(warning.Message);
                    }

                    await _agentStreamingContext.AddWorkflowAsync(
                        (callResponse.Successful ? "success" : "failed") + " calling tool " + toolCall.Name +
                        (callResponse.Successful ? "" : ", err: " + callResponse.ErrorMessage) +
                        " in " + sw.Elapsed.TotalMilliseconds.ToString("0.0") + "ms...",
                        ctx.CancellationToken);

                    if (ctx.CancellationToken.IsCancellationRequested) { return InvokeResult<IAgentPipelineContext>.Abort(); }
                    if (!callResponse.Successful) { return InvokeResult<IAgentPipelineContext>.FromInvokeResult(callResponse.ToInvokeResult()); }

                    var result = callResponse.Result;
                    ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCallResults.Add(result);

                    // once we've added at least one result, the guard will begin emitting countdown/warnings on subsequent repeats
                    hasAnyToolResultsThisTurn = true;
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
