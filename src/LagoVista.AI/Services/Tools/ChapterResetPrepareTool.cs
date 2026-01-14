using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Tool name: "chapter_reset_prepare"
    ///
    /// Minimal v1:
    /// - Accept a model-provided summary
    /// - Store it into CurrentCapsuleJson (as ContextCapsule)
    /// - Return capsuleJson + prepareToken for user approval
    ///
    /// Commit is performed by ChapterResetCommitTool.
    /// </summary>
    public class ChapterResetPrepareTool : IAgentTool
    {
        public const string ToolName = "chapter_reset_prepare";

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolSummary = "Prepare a chapter reset by generating/storing a capsule and returning a prepare token for user approval.";

        public const string ToolUsageMetadata = @"
Purpose
- Prepare a chapter boundary reset.
- Accept a model-provided summary and store it into the session capsule.
- Return a prepareToken + capsuleJson so the user can review/approve.

When to call
- Call when you believe the current chapter is complete and you want to start a new chapter.

Required inputs (JSON)
- summary (string): The model's chapter summary.

When preparing a new chapter, you MUST produce a capsule-ready summary for chapter_reset_prepare.summary: Format (required):
- Goal: one sentence
- Progress: 3–7 bullets of what was completed (facts only)
- Decisions: 0–5 bullets (include rationale if non-obvious)
- Constraints: 0–5 bullets (hard requirements/limitations)
- Touched Files: list file paths you changed or discussed (no code blocks)
- Open Questions: 0–5 bullets (must be actionable questions)
- Next Steps: 3–7 bullets (concrete, ordered) Rules:
- Be concise; no prose paragraphs.
- No fenced code blocks (```); only short inline snippets or method signatures when necessary.
- Do NOT invent file paths; only include files you saw in active files/TOC.
- If you are unsure about any item, omit it or mark it as “unknown” rather than guessing. Then call chapter_reset_prepare with summary containing exactly the above.
- If you cannot list at least 3 next steps or the goal is unknown, ask the user a clarification question before calling prepare.

Output
- JSON string envelope:
  { ok: true, prepareToken: string, capsuleJson: string, currentChapterIndex: number, turnCount: number, lastTurnId: string }

Rules
- Do not clear turns or advance chapter index; that is done by chapter_reset_commit.
";

        private readonly IAdminLogger _logger;

        public ChapterResetPrepareTool(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, ToolSummary, p =>
            {
                p.String("summary", "The model's chapter summary.", required: true);
            });
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            // This tool is executed via the AgentToolExecutionContext overload.
            return Task.FromResult(InvokeResult<string>.FromError("not_supported"));
        }

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            const string baseTag = "[ChapterResetPrepareTool]";

            try
            {
                var payload = JObject.Parse(argumentsJson ?? "{}");
                var summary = payload.Value<string>("summary")?.Trim();

                if (string.IsNullOrWhiteSpace(summary))
                    return InvokeResult<string>.FromError("summary is required");

                var turnCount = context.Session.Turns?.Count ?? 0;
                var lastTurnId = context.Session.Turns?.LastOrDefault()?.Id;

                // Minimal capsule: store summary in GitSummary for now (field already exists).
                // We can extend later with structured fields.
                var capsule = new ContextCapsule
                {
                    ChapterIndex = context.Session.CurrentChapterIndex,
                    ChapterTitle = $"Chapter {context.Session.CurrentChapterIndex}",
                    PreviousChapterSummary = summary,
                };

                context.Session.CurrentCapsule = capsule;

                // Strict prepare token includes lastTurnId.
                // Format: v1|<sessionId>|<chapterIndex>|<turnCount>|<lastTurnId>
                var prepareToken = $"v1|{context.Session.Id}|{context.Session.CurrentChapterIndex}|{turnCount}|{lastTurnId}";

                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["prepareToken"] = prepareToken,
                    ["capsuleJson"] = JsonConvert.SerializeObject( context.Session.CurrentCapsule),
                    ["currentChapterIndex"] = context.Session.CurrentChapterIndex,
                    ["turnCount"] = turnCount,
                    ["lastTurnId"] = lastTurnId,
                };

                return InvokeResult<string>.Create(envelope.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                _logger.AddException(baseTag, ex);
                return InvokeResult<string>.FromException(baseTag, ex);
            }
        }
    }
}
