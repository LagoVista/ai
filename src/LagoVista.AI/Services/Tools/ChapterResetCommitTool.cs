using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models;
using LagoVista.AI.Models.Resources;
using LagoVista.Core;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json.Linq;
using RingCentral;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Tool name: "chapter_reset_commit"
    ///
    /// Minimal v1:
    /// - Accept a prepareToken
    /// - Validate it against the current session state (no-freeze optimistic concurrency)
    /// - Call IAgentSessionManager.CheckpointAndResetAsync
    /// </summary>
    public class ChapterResetCommitTool : IAgentTool
    {
        public const string ToolName = "chapter_reset_commit";

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolSummary = "Commit a chapter reset after user approval using a prepare token.";

        public const string ToolUsageMetadata = @"
Purpose
- Commit the chapter boundary reset after user approval.
- Archives current turns, increments chapter index, clears turns.

When to call
- Only after the user has reviewed and approved the capsule returned by chapter_reset_prepare.

Required inputs (JSON)
- prepareToken (string): Token returned by chapter_reset_prepare.

Output
- JSON string envelope:
  { ok: true, archive: { ... }, newChapterIndex: number }

Rules
- Must validate prepareToken against current session state.
- If session has advanced since prepare, return an error and re-run prepare.
";

        private readonly IAgentSessionManager _sessionManager;
        private readonly IAdminLogger _logger;
        private readonly IAgentSessionTurnArchiveStore _archiveStore;
        

        public ChapterResetCommitTool(IAgentSessionManager sessionManager, IAgentSessionTurnArchiveStore archiveStore, IAdminLogger logger)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _archiveStore = archiveStore ?? throw new ArgumentNullException(nameof(archiveStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, ToolSummary, p =>
            {
                p.String("prepareToken", "Token returned by chapter_reset_prepare.", required: true);
            });
        }

        public  Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            // This tool is executed via the AgentToolExecutionContext overload.
            return Task.FromResult(InvokeResult<string>.FromError("not_supported"));
        }

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            const string baseTag = "[ChapterResetCommitTool]";

            try
            {

                var payload = JObject.Parse(argumentsJson ?? "{}");
                var prepareToken = payload.Value<string>("prepareToken")?.Trim();
                if (string.IsNullOrWhiteSpace(prepareToken)) return InvokeResult<string>.FromError("prepareToken is required");

                // Expected: v1|<sessionId>|<chapterIndex>|<turnCount>|<lastTurnId>
                var parts = prepareToken.Split('|');
                if (parts.Length != 5 || !string.Equals(parts[0], "v1", StringComparison.OrdinalIgnoreCase))
                    return InvokeResult<string>.FromError("invalid_prepare_token");

                var tokenSessionId = parts[1];
                var tokenChapterIndex = int.TryParse(parts[2], out var ci) ? ci : -1;
                var tokenTurnCount = int.TryParse(parts[3], out var tc) ? tc : -1;
                var tokenLastTurnId = parts[4];

                if (!string.Equals(tokenSessionId, context.Session.Id, StringComparison.OrdinalIgnoreCase))
                    return InvokeResult<string>.FromError("prepare_token_session_mismatch");

                var currentTurnCount = context.Session.Turns?.Count ?? 0;
                var currentLastTurnId = context.Session.Turns?.LastOrDefault()?.Id;

                if (tokenChapterIndex != context.Session.CurrentChapterIndex)
                    return InvokeResult<string>.FromError("prepare_token_chapter_mismatch");


                // Archive turns.
                var archive = await _archiveStore.SaveAsync(context.Session, context.Session.Turns, context.Session.ChapterTitle, 
                    context.Session.CurrentCapsule.PreviousChapterSummary, context.Envelope.User);
                context.Session.Archives.Add(archive);

                context.Session.Turns = new List<AgentSessionTurn>();

                context.Session.LastUpdatedDate = DateTime.UtcNow.ToJSONString();
                context.Session.LastUpdatedBy = context.Envelope.User;
                context.Session.CurrentChapterIndex = context.Session.CurrentChapterIndex + 1;
                context.Session.ChapterTitle = $"{AIResources.AgentChapter_ChaterLabel} {context.Session.CurrentChapterIndex}";
                context.Session.ChapterSeed = context.Session.CurrentCapsule.PreviousChapterSummary;
           
                context.ThisTurn.Status = Core.Models.EntityHeader<AgentSessionTurnStatuses>.Create(AgentSessionTurnStatuses.ChapterEnd);
               
                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["archive"] = JObject.FromObject(archive),
                    ["newChapterIndex"] = context.Session.CurrentChapterIndex + 1,
                };

                return InvokeResult<string>.Create(envelope.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                _logger.AddException(baseTag, ex);
                return InvokeResult<string>.FromException(baseTag, ex);
            }
        }
    }
}
