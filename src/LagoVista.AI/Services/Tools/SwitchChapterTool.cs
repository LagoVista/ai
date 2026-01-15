using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Switches the current session to a different chapter by chapter id.
    /// </summary>
    public sealed class SwitchChapterTool : IAgentTool
    {
        public const string ToolName = "chapter_switch_by_id";
        public const string ToolSummary = "Switch the current session to a different chapter by chapter id.";
        public const string ToolUsageMetadata = "Switches the session to a different chapter by calling the session manager's RestoreSessionChapterAsync overload.";

        private readonly IAdminLogger _logger;
        private readonly IAgentSessionManager _sessionManager;

        public SwitchChapterTool(IAdminLogger logger, IAgentSessionManager sessionManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        }

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        private sealed class SwitchChapterArgs
        {
            [JsonProperty("chapterId")]
            public string ChapterId { get; set; }
        }

        private sealed class SwitchChapterResult
        {
            [JsonProperty("sessionId")]
            public string SessionId { get; set; }

            [JsonProperty("fromChapterId")]
            public string FromChapterId { get; set; }

            [JsonProperty("toChapterId")]
            public string ToChapterId { get; set; }

            [JsonProperty("currentChapterIndex")]
            public int CurrentChapterIndex { get; set; }

            [JsonProperty("currentChapterTitle")]
            public string CurrentChapterTitle { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)

        {
            if (context == null) return InvokeResult<string>.FromError("SwitchChapterTool requires a pipeline context.");

            try
            {
                if (string.IsNullOrWhiteSpace(argumentsJson))
                    return InvokeResult<string>.FromError("SwitchChapterTool requires a non-empty arguments object.");

                var args = JsonConvert.DeserializeObject<SwitchChapterArgs>(argumentsJson) ?? new SwitchChapterArgs();
                if (string.IsNullOrWhiteSpace(args.ChapterId))
                    return InvokeResult<string>.FromError("SwitchChapterTool requires a non-empty 'chapterId' string.");

                if (context.Session == null)
                    return InvokeResult<string>.FromError("No session is available on the pipeline context.");

                var fromChapterId = context.Session.CurrentChapter?.Id;

                // NOTE: You indicated tools should not persist; this manager method should only mutate the in-memory session.
                var switchResult = await _sessionManager.RestoreSessionChapterAsync(
                    context.Session,
                    args.ChapterId.Trim(),
                    context.Envelope?.Org,
                    context.Envelope?.User);

                if (!switchResult.Successful)
                    return InvokeResult<string>.FromInvokeResult(switchResult.ToInvokeResult());

                // Ensure the pipeline context still points at the mutated session instance.
                context.AttachSession(context.Session, context.ThisTurn);

                var result = new SwitchChapterResult
                {
                    SessionId = context.Session.Id,
                    FromChapterId = fromChapterId,
                    ToChapterId = context.Session.CurrentChapter?.Id,
                    CurrentChapterIndex = context.Session.CurrentChapterIndex,
                    CurrentChapterTitle = context.Session.CurrentChapter?.Text,
                };

                return InvokeResult<string>.Create(JsonConvert.SerializeObject(result));
            }
            catch (Exception ex)
            {
                _logger.AddException("[SwitchChapterTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("SwitchChapterTool failed.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Switch the current session to a different chapter by chapter id.",
                p =>
                {
                    p.String("chapterId", "The chapter id to switch to.", required: true);
                });
        }
    }
}
