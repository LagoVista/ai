using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Agent tool that updates the detailed body text for a single DDR chapter.
    /// Tool name: "update_chapter_details".
    /// </summary>
    public class UpdateChapterDetailsAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "update_chapter_details";
        public UpdateChapterDetailsAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger) : base(ddrManager, adminLogger)
        {
        }

        public const string ToolSummary = "udpate the detail of a chapter in a ddr";
        public override string Name => ToolName;

        public const string ToolUsageMetadata = "Replaces or updates the detailed body content of a DDR chapter. Suitable for elaborating, reorganizing, or correcting chapter details.";
        protected override string Tag => "[UpdateChapterDetailsAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Update the detailed body text for a specific DDR chapter (the full spec content, not the short summary).", p =>
            {
                p.String("identifier", "DDR identifier in TLA-### format, for example 'SYS-001'.", required: true);
                p.String("chapter_id", "Stable chapter identifier returned from add_chapter/add_chapters/list_chapters.", required: true);
                p.String("details", "Full chapter body text. This can be multi-paragraph markdown, but should stay focused on this chapter only.", required: true);
            });
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(JObject payload, AgentToolExecutionContext context, CancellationToken cancellationToken)
        {
            const string baseTag = "[UpdateChapterDetailsAgentTool__Execute]";
            var identifier = payload.Value<string>("identifier")?.Trim();
            var chapterId = payload.Value<string>("chapter_id")?.Trim();
            var details = payload.Value<string>("details") ?? string.Empty;
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

                var chapter = ddr.Chapters?.FirstOrDefault(c => c.Id == chapterId);
                if (chapter == null)
                {
                    return FromError($"Chapter '{chapterId}' not found.");
                }

                chapter.Details = details;
                await _ddrManager.UpdateDdrAsync(ddr, context.Org, context.User);
                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["identifier"] = identifier,
                        ["chapter_id"] = chapterId
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