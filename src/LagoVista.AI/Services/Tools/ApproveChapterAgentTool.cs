using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Agent tool that approves an individual DDR chapter.
    /// Tool name: "approve_chapter".
    /// </summary>
    public class ApproveChapterAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "approve_chapter";

        public ApproveChapterAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger)
            : base(ddrManager, adminLogger)
        {
        }

        public const string ToolUsageMetadata =
    "Approves a DDR chapter once its content is complete. Marks the chapter as finalized. Requires explicit user approval.";

        public override string Name => ToolName;

        public const string ToolSummary = "approve a single chapter within a ddr";


        protected override string Tag => "[ApproveChapterAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Approve a specific DDR chapter, recording approver and timestamp. Use this after the chapter content is stable.",
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
                        }
                    },
                    required = new[] { "identifier", "chapter_id" }
                }
            };

            return schema;
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(
            JObject payload,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken)
        {
            const string baseTag = "[ApproveChapterAgentTool__Execute]";

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

                var now = DateTime.UtcNow.ToJSONString();

                if (chapter.ApprovedBy != null && !string.IsNullOrWhiteSpace(chapter.ApprovedTimestamp))
                {
                    var existingEnvelope = new JObject
                    {
                        ["ok"] = true,
                        ["result"] = new JObject
                        {
                            ["identifier"] = identifier,
                            ["chapter_id"] = chapterId,
                            ["approved_by"] = new JObject
                            {
                                ["id"] = chapter.ApprovedBy.Id,
                                ["text"] = chapter.ApprovedBy.Text
                            },
                            ["approved_timestamp"] = chapter.ApprovedTimestamp
                        }
                    };

                    return FromEnvelope(existingEnvelope);
                }

                chapter.ApprovedBy = context.User;
                chapter.ApprovedTimestamp = now;

                await _ddrManager.UpdateDdrAsync(ddr, context.Org, context.User);

                var envelopeResult = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["identifier"] = identifier,
                        ["chapter_id"] = chapterId,
                        ["approved_by"] = new JObject
                        {
                            ["id"] = context.User.Id,
                            ["text"] = context.User.Text
                        },
                        ["approved_timestamp"] = now
                    }
                };

                return FromEnvelope(envelopeResult);
            }
            catch (Exception ex)
            {
                _adminLogger.AddException(baseTag, ex);
                return InvokeResult<string>.FromException(baseTag, ex);
            }
        }
    }
}
