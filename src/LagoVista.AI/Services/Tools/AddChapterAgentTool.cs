using System;
using System.Collections.Generic;
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
    /// Agent tool that adds a single chapter to an existing DDR.
    /// Tool name: "add_chapter".
    /// </summary>
    public class AddChapterAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "add_chapter";

        public AddChapterAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger)
            : base(ddrManager, adminLogger)
        {
        }

        public const string ToolUsageMetadata =
    "Adds multiple chapters to a DDR in a single operation. Use when initializing a DDR's chapter structure or importing a predefined outline.";


        public override string Name => ToolName;

        protected override string Tag => "[AddChapterAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Add a new chapter (title + summary) to an existing DDR.",
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
                        title = new
                        {
                            type = "string",
                            description = "Chapter title. Keep this concise and descriptive."
                        },
                        summary = new
                        {
                            type = "string",
                            description = "Short 50K-foot-level chapter summary (one or two sentences)."
                        }
                    },
                    required = new[] { "identifier", "title", "summary" }
                }
            };

            return schema;
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(
            JObject payload,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken)
        {
            const string baseTag = "[AddChapterAgentTool__Execute]";

            var identifier = payload.Value<string>("identifier")?.Trim();
            var title = payload.Value<string>("title")?.Trim();
            var summary = payload.Value<string>("summary")?.Trim();

            if (string.IsNullOrWhiteSpace(identifier))
            {
                return FromError("identifier is required.");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return FromError("title is required.");
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

                if (ddr.Chapters == null)
                {
                    ddr.Chapters = new List<DdrChapter>();
                }

                var chapter = new DdrChapter
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = title,
                    Summary = summary,
                    Details = string.Empty,
                    ApprovedBy = null,
                    ApprovedTimestamp = null
                };

                ddr.Chapters.Add(chapter);

                await _ddrManager.UpdateDdrAsync(ddr, context.Org, context.User);

                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["identifier"] = identifier,
                        ["chapter_id"] = chapter.Id,
                        ["title"] = title,
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
