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
using static System.Collections.Specialized.BitVector32;

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
            var newChapter = ctx.ThisTurn.Type.Value == AgentSessionTurnType.ChapterStart;

            var apkResult = await _apkProvider.CreateAsync(ctx, newChapter || changeMode);
            if (!apkResult.Successful) return InvokeResult<IAgentPipelineContext>.FromInvokeResult(apkResult.ToInvokeResult());

            _adminLogger.Trace($"{this.Tag()} populate, mode change {changeMode}, new chapter {newChapter}.");

            var apk = apkResult.Result;

            ctx.PromptKnowledgeProvider.ClearSession();
            ctx.PromptKnowledgeProvider.ClearConsumables();
            ctx.PromptKnowledgeProvider.ActiveTools.Clear();

            foreach (var key in apk.KindCatalog.Keys)
            {
                var items = apk.KindCatalog[key];

                if (items.ConsumableKnowledge.Items.Any())
                {
                    var register = ctx.PromptKnowledgeProvider.GetOrCreateRegister(key, Models.Context.ContextClassification.Consumable);
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
                    var register = ctx.PromptKnowledgeProvider.GetOrCreateRegister(key, Models.Context.ContextClassification.Session);
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


            if (ctx.RagContent.Any())
            {
                var ragRegister = ctx.PromptKnowledgeProvider.GetOrCreateRegister(AI.Models.KnowledgeKind.Rag, AI.Models.Context.ContextClassification.Consumable);
                var sb = new StringBuilder();
                sb.AppendLine("<RAG_CONTEXT>");
                sb.AppendLine("Rules:");
                sb.AppendLine("1) Treat exhibits as retrieved reference text.");
                sb.AppendLine("2) If the answer isn’t supported by exhibits, say so.");
                sb.AppendLine("3) Cite exhibits like [R1], [R2] next to the relevant claims.");
                sb.AppendLine();

                foreach (var ragContent in ctx.RagContent)
                    sb.AppendLine(ragContent.ToContentBlock());

                sb.AppendLine("</RAG_CONTEXT>");
            
                ragRegister.Add(sb.ToString());

                _adminLogger.Trace($"{this.Tag()} added {ctx.RagContent.Count()} rag items.");
            }

            // ---------------------------------------------------------------------
            // NEW CHAPTER REHYDRATE (inject capsule after /chapter/reset)
            // Condition: no turns + archives exist + capsule exists
            // ---------------------------------------------------------------------
            if (newChapter)
            {
                var capsuleBlock = new StringBuilder();
                capsuleBlock.AppendLine("## NEW CHAPTER CONTINUATION (AUTHORITATIVE)");
                capsuleBlock.AppendLine("You are continuing a multi-chapter session.");
                capsuleBlock.AppendLine("You do NOT have access to prior chapter turns.");
                capsuleBlock.AppendLine("Treat the following capsule JSON as the ONLY authoritative summary of prior chapters.");
                capsuleBlock.AppendLine("If details are missing, ask clarifying questions rather than guessing.");
                capsuleBlock.AppendLine();
                capsuleBlock.AppendLine($"CurrentChapterIndex: {ctx.Session.CurrentChapterIndex}");
                capsuleBlock.AppendLine("ContextCapsuleJson:");
                capsuleBlock.AppendLine($"Title: {ctx.Session.CurrentCapsule.ChapterTitle}");
                capsuleBlock.AppendLine($"Idx: {ctx.Session.CurrentCapsule.ChapterIndex}");
                capsuleBlock.AppendLine();

                var newChapterRegister = ctx.PromptKnowledgeProvider.GetOrCreateRegister(KnowledgeKind.NewChapterInitialPrompt, Models.Context.ContextClassification.Session);
                newChapterRegister.Add(capsuleBlock.ToString());
  
                var touchedFilesLog = new StringBuilder();
                touchedFilesLog.AppendLine("## RECENTLY TOUCHED FILES");
                touchedFilesLog.AppendLine("- The following files were recently edited in the last chapter. Do not assume their presence or content, but you may reference them if relevant.");
                touchedFilesLog.AppendLine("- You may use workspace_read_client_file to read their current content if needed.");
                touchedFilesLog.AppendLine("- You may call the workspace_toc_get to get the complete directory structure and files.");
                foreach(var file in ctx.Session.TouchedFiles)
                {
                    touchedFilesLog.AppendLine($"- {file.Path} - SHA256={file.ContentHash} - Last Access={file.LastAccess}");
                }
                newChapterRegister.Add(touchedFilesLog.ToString());
  
                var previousChapterSummary = new StringBuilder();
                previousChapterSummary.AppendLine("## PREVIOUS CHAPTERS SUMMARY");
                previousChapterSummary.AppendLine("- The following is a summary of prior chapters for context.");
                previousChapterSummary.AppendLine("- Do NOT assume any details beyond what is provided here.");
                previousChapterSummary.AppendLine("- If details are missing, ask clarifying questions rather than guessing.");
                previousChapterSummary.AppendLine(ctx.Session.CurrentCapsule.PreviousChapterSummary);
                previousChapterSummary.AppendLine("---");
                previousChapterSummary.AppendLine();
        
                newChapterRegister.Add(previousChapterSummary.ToString());

                _adminLogger.Trace($"{this.Tag()} added new chapter summary");
            }

            ctx.PromptKnowledgeProvider.ActiveTools.AddRange(apk.ActiveTools);

            var modeBlock =
$@"## CURRENT Status 
- Mode Key (authoritative): {ctx.Mode.Key}
- Chapter: {ctx.Session.ChapterTitle}
- Display Name (non-authoritative): {ctx.Mode.Name}
- MUST NEVER call the agent_change_mode tool with the parameter [{ctx.Mode.Key}] as we are already in that mode.
- The assistant MUST NOT call agent_change_mode inside any multi/parallel tool wrapper. Mode changes must be a single direct call only when required
- Never call agent_change_mode as a “keep alive”, “no - op safeguard”, or “continue” action.";

            var modeRegister = ctx.PromptKnowledgeProvider.GetOrCreateRegister(KnowledgeKind.AgentModelContext, Models.Context.ContextClassification.Session);
            modeRegister.Add(modeBlock);

            var currentBranch = String.IsNullOrEmpty(ctx.Session.CurrentBranch) ? AgentSession.DefaultBranch : ctx.Session.CurrentBranch;

            if (!ctx.Session.Kfrs.ContainsKey(currentBranch))
            {
                var kfrBlock = @$"
## BEGIN Known Facts Registry (KFR) — Active Working Memory

These entries are authoritative for near-term correctness.
They may be replaced or removed at any time.

For agent/session state, rely only on KFR.
”
### Goal (single): -
### Plan (single): -
### ActiveContracts: -
### Constraints: -
### OpenQuestions (RequiresResolution): -
## END Known Facts Registry (KFR) — Active Working Memory
";
                var kfrRegister = ctx.PromptKnowledgeProvider.GetOrCreateRegister(KnowledgeKind.Kfr, Models.Context.ContextClassification.Session);
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
                var kfrRegister = ctx.PromptKnowledgeProvider.GetOrCreateRegister(KnowledgeKind.Kfr, Models.Context.ContextClassification.Session);
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
