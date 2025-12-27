using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Agent tool that deletes a single DDR chapter.
    /// Tool name: "delete_chapter".
    /// </summary>
    public class DeleteChapterAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "delete_chapter";
        public DeleteChapterAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger) : base(ddrManager, adminLogger)
        {
        }

        public const string ToolUsageMetadata = "Deletes a chapter from a DDR. Should be used cautiously and only when a chapter is no longer relevant.";
        public const string ToolSummary = "delete a chapter within a ddr";
        public override string Name => ToolName;
        protected override string Tag => "[DeleteChapterAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Delete a specific DDR chapter by chapter_id.", p =>
            {
                p.String("identifier", "DDR identifier in TLA-### format, for example 'SYS-001'.", required: true);
                p.String("chapter_id", "Stable chapter identifier returned from add_chapter/add_chapters/list_chapters.", required: true);
            });
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(JObject payload, AgentToolExecutionContext context, CancellationToken cancellationToken)
        {
            const string baseTag = "[DeleteChapterAgentTool__Execute]";
            var identifier = payload.Value<string>("identifier")?.Trim();
            var chapterId = payload.Value<string>("chapter_id")?.Trim();
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return FromError("identifier is required.");
            }

            if (string.IsNullOrWhiteSpace(chapterId))
            {
                return FromError("chapter_id is required.");
            }

            try
            {
                var ddr = await _ddrManager.GetDdrByTlaIdentiferAsync(identifier, context.Org, context.User);
                if (ddr == null)
                {
                    return FromError($"DDR '{identifier}' not found.");
                }

                var chapters = ddr.Chapters ?? new List<DdrChapter>();
                var chapter = chapters.FirstOrDefault(c => c.Id == chapterId);
                if (chapter == null)
                {
                    return FromError($"Chapter '{chapterId}' not found.");
                }

                chapters.Remove(chapter);
                ddr.Chapters = chapters;
                await _ddrManager.UpdateDdrAsync(ddr, context.Org, context.User);
                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["identifier"] = identifier,
                        ["chapter_id"] = chapterId,
                        ["deleted"] = true
                    }
                };
                return FromEnvelope(envelope);
            }
            catch (Exception ex)
            {
                _adminLogger.AddException(baseTag, ex);
                return InvokeResult<string>.FromException(baseTag, ex);
            }
        }
    }
}