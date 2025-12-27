using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Managers;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Pipeline
{
    /// <summary>
    /// AGN-032 Step: ContextProviderInitializer
    ///
    /// Expects:
    /// - <see cref="AgentPipelineContext.Session"/>, <see cref="AgentPipelineContext.ThisTurn"/>, and <see cref="AgentPipelineContext.Request"/> are present.
    /// - <see cref="AgentPipelineContext.AgentContext"/> and (optionally) <see cref="AgentPipelineContext.ConversationContext"/> are resolved.
    ///
    /// Updates:
    /// - Initializes session/mode-based context providers required by downstream execution (RAG, tool catalogs, prompt inputs, etc.).
    /// - Does not produce a response; it prepares state for downstream steps.
    ///
    /// Branching:
    /// - If <see cref="AgentPipelineContext.Type"/> is <see cref="AgentPipelineContextTypes.ClientToolCallContinuation"/>,
    ///   route to <see cref="IClientToolContinuationResolverPipelineStep"/>.
    /// - Otherwise (Initial / FollowOn), route to <see cref="IAgentReasonerPipelineStep"/>.
    ///
    /// Next:
    /// - ClientToolContinuationResolver OR AgentReasoner
    /// </summary>
    public sealed class PromptKnowledgeProviderInitializerPipelineStep : PipelineStep, IPromptKnowledgeProviderInitializerPipelineStep
    {
        private readonly IAdminLogger _adminLogger;
        private readonly IAgentKnowledgePackService _apkProvider;
        private readonly IServerToolSchemaProvider _toolSchemaProvider;

        public PromptKnowledgeProviderInitializerPipelineStep(
            IAgentReasonerPipelineStep next,
            IAgentKnowledgePackService apkProvider,
            IAgentPipelineContextValidator validator,
            IServerToolSchemaProvider toolSchemaProvider,
            IAdminLogger adminLogger) : base(next, validator, adminLogger)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _apkProvider = apkProvider ?? throw new ArgumentNullException(nameof(apkProvider));
            _toolSchemaProvider = toolSchemaProvider ?? throw new ArgumentNullException(nameof(toolSchemaProvider));
        }

        protected override PipelineSteps StepType => PipelineSteps.PromptKnowledgeProviderInitializer;

        protected override async Task<InvokeResult<IAgentPipelineContext>> ExecuteStepAsync(IAgentPipelineContext ctx)
        {
            var apkResult = await _apkProvider.CreateAsync(ctx.Envelope.Org.Id, ctx.AgentContext, ctx.Envelope.ConversationContextId, ctx.Session.Mode);
            var apk = apkResult.Result;

            foreach(var key in apk.KindCatalog.Keys)
            {
                var items = apk.KindCatalog[key];

                if (items.ConsumableKnowledge.Items.Any())
                {
                    var register = ctx.PromptKnowledgeProvider.GetOrCreateRegister(key.ToString(), Models.Context.ContextClassification.Consumable);
                    var contentBlock = new StringBuilder();
                    contentBlock.AppendLine(items.BeginMarker);
                    contentBlock.AppendLine(items.InstructionLine);
                    foreach (var content in items.ConsumableKnowledge.Items)
                    {
                        contentBlock.AppendLine(content.Content);
                    }
                    contentBlock.AppendLine(items.EndMarker);
                    register.Add(contentBlock.ToString());
                }

                if (items.SessionKnowledge.Items.Any())
                {
                    var register = ctx.PromptKnowledgeProvider.GetOrCreateRegister(key.ToString(), Models.Context.ContextClassification.Session);
                    var contentBlock = new StringBuilder();
                    contentBlock.AppendLine(items.BeginMarker);
                    contentBlock.AppendLine(items.InstructionLine);
                    foreach (var content in items.SessionKnowledge.Items)
                    {
                        contentBlock.AppendLine(content.Content);
                    }
                    contentBlock.AppendLine(items.EndMarker);
                    Console.WriteLine($"---------\r\n{contentBlock.ToString()}\r\n---------------------------\r\n)"); 

                    register.Add(contentBlock.ToString());
                }
            }

            var currentBranch = String.IsNullOrEmpty(ctx.Session.CurrentBranch) ? AgentSession.DefaultBranch : ctx.Session.CurrentBranch;

            if (!ctx.Session.Kfrs.ContainsKey(currentBranch))
            {
                var kfrBlock = @$"
## BEGIN Known Facts Registry (KFR) — Active Working Memory

These entries are authoritative for near-term correctness.
They may be replaced or removed at any time.

Do not infer or assume facts outside this registry.

### Goal (single)
 - empty

### Plan (single)
 - empty

### ActiveContracts
 - empty

### Constraints
 - empty

### OpenQuestions (RequiresResolution)
 - empty

## END Known Facts Registry (KFR) — Active Working Memory
";
                var kfrRegister = ctx.PromptKnowledgeProvider.GetOrCreateRegister("kfr", Models.Context.ContextClassification.Session);
                kfrRegister.Add(kfrBlock);
            }
            else
            {
                var branchKfrs = ctx.Session.Kfrs[ctx.Session.CurrentBranch];

                foreach (var toolName in apk.EnabledToolNames)
                {
                    var schema = _toolSchemaProvider.GetToolSchema(toolName);

                    ctx.PromptKnowledgeProvider.AvailableToolSchemas.Add(toolName, schema);
                }

                var kfrBlock = @$"
## BEGIN Known Facts Registry (KFR) — Active Working Memory

These entries are authoritative for near-term correctness.
They may be replaced or removed at any time.

Do not infer or assume facts outside this registry.

### Goal (single)
{BuildKfrSection(branchKfrs.Where(kfr => kfr.Kind == KfrKind.Goal && kfr.IsActive))}

### Plan (single)
{BuildKfrSection(branchKfrs.Where(kfr => kfr.Kind == KfrKind.Plan && kfr.IsActive))}

### ActiveContracts
{BuildKfrSection(branchKfrs.Where(kfr => kfr.Kind == KfrKind.ActiveContract && kfr.IsActive))}

### Constraints
{BuildKfrSection(branchKfrs.Where(kfr => kfr.Kind == KfrKind.Constraint && kfr.IsActive))}

### OpenQuestions (RequiresResolution)
{BuildKfrSection(branchKfrs.Where(kfr => kfr.Kind == KfrKind.OpenQuestion && kfr.IsActive))}

## END Known Facts Registry (KFR) — Active Working Memory
";
                var kfrRegister = ctx.PromptKnowledgeProvider.GetOrCreateRegister("kfr", Models.Context.ContextClassification.Session);
                kfrRegister.Add(kfrBlock);
            }
            return InvokeResult<IAgentPipelineContext>.Create(ctx);
        }

        private string BuildKfrSection(IEnumerable<AgentSessionKfrEntry> entries)
        {
            if (!entries.Any())
                return " - empty";

            var builder = new StringBuilder();
            foreach(var entry in entries)
            {
                builder.AppendLine($"- {entry.Value}");
            }

            return builder.ToString();
        }
    }
}
