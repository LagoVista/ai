using System;
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
    /// Agent tool that updates the detailed body text for a single DDR chapter.
    /// Tool name: "update_chapter_details".
    /// </summary>
    public class UpdateChapterDetailsAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "update_chapter_details";

        public UpdateChapterDetailsAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger)
            : base(ddrManager, adminLogger)
        {
        }

        public override string Name => ToolName;

        public const string ToolUsageMetadata =
    "Replaces or updates the detailed body content of a DDR chapter. Suitable for elaborating, reorganizing, or correcting chapter details.";


        protected override string Tag => "[UpdateChapterDetailsAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Update the detailed body text for a specific DDR chapter (the full spec content, not the short summary).",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        identifier = new
                        {
                            type = "string",
                            description = "DDR identifier in TLA-### format, for example 'SYS-001'."
                        },
                        chapter_id = new
                        {
                            type = "string",
                            description = "Stable chapter identifier returned from add_chapter/add_chapters/list_chapters."
                        },
                        details = new
                        {
                            type = "string",
                            description = "Full chapter body text. This can be multi-paragraph markdown, but should stay focused on this chapter only."
                        }
                    },
                    required = new[] { "identifier", "chapter_id", "details" }
                }
            };

            return schema;
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(
            JObject payload,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken)
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
                var ddr = await _ddrManager.GetDdrByTlaIdentiferAsync(
                    identifier,
                    context.Org,
                    context.User);

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
