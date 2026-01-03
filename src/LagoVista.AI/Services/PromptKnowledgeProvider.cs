using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Services;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Services
{
    public class PromptKnowledgeProvider : IPromptKnowledgeProvider
    {

        private readonly IAdminLogger _adminLogger;
        private readonly IAgentKnowledgePackService _apkProvider;

        public PromptKnowledgeProvider(IAgentKnowledgePackService apkProvider, IAdminLogger adminLogger)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _apkProvider = apkProvider ?? throw new ArgumentNullException(nameof(apkProvider));
        }

        public async Task<InvokeResult<IAgentPipelineContext>> PopulateAsync(IAgentPipelineContext ctx, bool changeMode)
        {
            var apkResult = await _apkProvider.CreateAsync(ctx, false);
            var apk = apkResult.Result;

            foreach (var key in apk.KindCatalog.Keys)
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
 - no goals currently exist

### Plan (single)
 - no plans currently exist

### ActiveContracts
 - no active contracts exist

### Constraints
 - no constraints exist

### OpenQuestions (RequiresResolution)
 - no open questions exist

## END Known Facts Registry (KFR) — Active Working Memory
";
                var kfrRegister = ctx.PromptKnowledgeProvider.GetOrCreateRegister("kfr", Models.Context.ContextClassification.Session);
                kfrRegister.Add(kfrBlock);
            }
            else
            {
                var branchKfrs = ctx.Session.Kfrs[ctx.Session.CurrentBranch];

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

            _adminLogger.Trace($"[JSON.PKP]={JsonConvert.SerializeObject(ctx.PromptKnowledgeProvider)}");

            return InvokeResult<IAgentPipelineContext>.Create(ctx);
        }

        private string BuildKfrSection(IEnumerable<AgentSessionKfrEntry> entries)
        {
            if (!entries.Any())
                return " - no entires exist";

            var builder = new StringBuilder();
            foreach (var entry in entries)
            {
                builder.AppendLine($"- {entry.Value}");
            }

            return builder.ToString();
        }
    }
}
