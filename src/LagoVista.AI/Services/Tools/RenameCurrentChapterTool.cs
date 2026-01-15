using System;
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
    /// Accepts a new title for the current chapter.
    /// NOTE: This tool intentionally does not mutate session state; wiring will be handled externally.
    /// </summary>
    public sealed class RenameCurrentChapterTool : IAgentTool
    {
        public const string ToolName = "chapter_rename_current";
        public const string ToolSummary = "Rename the current chapter.";
        public const string ToolUsageMetadata = "Use when the user asks to rename the current chapter. Returns the requested new title.";

        private readonly IAdminLogger _logger;

        public RenameCurrentChapterTool(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        private sealed class RenameCurrentChapterArgs
        {
            [JsonProperty("title")]
            public string Title { get; set; }
        }

        private sealed class RenameCurrentChapterResult
        {
            [JsonProperty("sessionId")]
            public string SessionId { get; set; }

            [JsonProperty("currentChapterId")]
            public string CurrentChapterId { get; set; }

            [JsonProperty("requestedTitle")]
            public string RequestedTitle { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {            
            throw new NotImplementedException();
        }
        
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context){
            try
            {
                if (string.IsNullOrWhiteSpace(argumentsJson))
                    return Task.FromResult(InvokeResult<string>.FromError("RenameCurrentChapterTool requires a non-empty arguments object."));

                var args = JsonConvert.DeserializeObject<RenameCurrentChapterArgs>(argumentsJson) ?? new RenameCurrentChapterArgs();
                if (string.IsNullOrWhiteSpace(args.Title))
                    return Task.FromResult(InvokeResult<string>.FromError("RenameCurrentChapterTool requires a non-empty 'title' string."));

                var session = context.Session;

                var result = new RenameCurrentChapterResult
                {
                    SessionId = session?.Id,
                    CurrentChapterId = session?.CurrentChapter?.Id,
                    RequestedTitle = args.Title.Trim(),
                };

                var chapter = session.Chapters.FirstOrDefault(chp=>chp.Id == session.CurrentChapter.Id);    
                if (chapter == null)
                    return Task.FromResult(InvokeResult<string>.FromError("RenameCurrentChapterTool failed."));

                chapter.Title = args.Title.Trim();
                session.CurrentChapter.Text = args.Title.Trim();

                return Task.FromResult(InvokeResult<string>.Create(JsonConvert.SerializeObject(result)));
            }
            catch (Exception ex)
            {
                _logger.AddException("[RenameCurrentChapterTool_ExecuteAsync__Exception]", ex);
                return Task.FromResult(InvokeResult<string>.FromError("RenameCurrentChapterTool failed."));
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Rename the current chapter by providing a new title.",
                p =>
                {
                    p.String("title", "New title for the current chapter.", required: true);
                });
        }
    }
}
