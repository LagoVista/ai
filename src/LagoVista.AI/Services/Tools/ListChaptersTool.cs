using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Lists chapters on the current session.
    /// </summary>
    public sealed class ListChaptersTool : IAgentTool
    {
        public const string ToolName = "list_chapters";
        public const string ToolSummary = "List chapters on the current agent session.";
        public const string ToolUsageMetadata = "Lists chapters on the current session (id, title, index) and indicates the current chapter.";

        private readonly IAdminLogger _logger;

        public ListChaptersTool(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        private sealed class ListChaptersArgs
        {
            // Intentionally empty for now.
        }

        private sealed class ChapterItem
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("title")]
            public string Title { get; set; }

            [JsonProperty("chapterIndex")]
            public int ChapterIndex { get; set; }

            [JsonProperty("isCurrent")]
            public bool IsCurrent { get; set; }
        }

        private sealed class ListChaptersResult
        {
            [JsonProperty("sessionId")]
            public string SessionId { get; set; }

            [JsonProperty("currentChapterId")]
            public string CurrentChapterId { get; set; }

            [JsonProperty("currentChapterIndex")]
            public int CurrentChapterIndex { get; set; }

            [JsonProperty("chapters")]
            public List<ChapterItem> Chapters { get; set; } = new List<ChapterItem>();
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
            
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            try
            {
                // Accept empty args.
                _ = JsonConvert.DeserializeObject<ListChaptersArgs>(argumentsJson ?? "{}") ?? new ListChaptersArgs();

                var session = context.Session;
                if (session == null)
                    return Task.FromResult(InvokeResult<string>.FromError("No session is available on the tool execution context."));

                var chapters = session.Chapters ?? new List<AgentSessionChapter>();

                var result = new ListChaptersResult
                {
                    SessionId = session.Id,
                    CurrentChapterId = session.CurrentChapter?.Id,
                    CurrentChapterIndex = session.CurrentChapterIndex,
                    Chapters = chapters
                        .Where(c => c != null)
                        .OrderBy(c => c.ChapterIndex)
                        .Select(c => new ChapterItem
                        {
                            Id = c.Id,
                            Title = c.Title,
                            ChapterIndex = c.ChapterIndex,
                            IsCurrent = string.Equals(c.Id, session.CurrentChapter?.Id, StringComparison.OrdinalIgnoreCase)
                        })
                        .ToList()
                };

                return Task.FromResult(InvokeResult<string>.Create(JsonConvert.SerializeObject(result)));
            }
            catch (Exception ex)
            {
                _logger.AddException("[ListChaptersTool_ExecuteAsync__Exception]", ex);
                return Task.FromResult(InvokeResult<string>.FromError("ListChaptersTool failed."));
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "List chapters on the current agent session.",
                p =>
                {
                    // No parameters.
                });
        }
    }
}
