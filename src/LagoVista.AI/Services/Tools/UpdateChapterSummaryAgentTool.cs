using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Agent tool that updates the summary text for a single DDR chapter.
    /// Tool name: "update_chapter_summary".
    /// </summary>
    public class UpdateChapterSummaryAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "update_chapter_summary";

        public UpdateChapterSummaryAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger)
            : base(ddrManager, adminLogger)
        {
        }

        public const string ToolUsageMetadata =
    "Updates the summary text for an existing DDR chapter. Used during refinement to keep chapter descriptions accurate and helpful.";
        
        public const string ToolSummary = "udpate the summary of a chapter in a ddr";


        public override string Name => ToolName;

        protected override string Tag => "[UpdateChapterSummaryAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Update the short 50K-foot summary for a specific DDR chapter.",
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
                        summary = new
                        {
                            type = "string",
                            description = "Updated chapter summary at the 50K-foot level (one or two sentences)."
                        }
                    },
                    required = new[] { "identifier", "chapter_id", "summary" }
                }
            };

            return schema;
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(
            JObject payload,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken)
        {
            const string baseTag = "[UpdateChapterSummaryAgentTool__Execute]";

            var identifier = payload.Value<string>("identifier")?.Trim();
            var chapterId = payload.Value<string>("chapter_id")?.Trim();
            var summary = payload.Value<string>("summary")?.Trim();

            if (string.IsNullOrWhiteSpace(identifier))
            {
                return FromError("identifier is required.");
            }

            if (string.IsNullOrWhiteSpace(chapterId))
            {
                return FromError("chapter_id is required.");
            }

            if (string.IsNullOrWhiteSpace(summary))
            {
                return FromError("summary is required.");
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

                chapter.Summary = summary;

                await _ddrManager.UpdateDdrAsync(ddr, context.Org, context.User);

                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["identifier"] = identifier,
                        ["chapter_id"] = chapterId,
                        ["summary"] = summary
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
