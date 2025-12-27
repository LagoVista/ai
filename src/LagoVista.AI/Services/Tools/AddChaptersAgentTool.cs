using System;
using System.Collections.Generic;
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
    /// Agent tool that adds multiple chapters to an existing DDR in a single call.
    /// Tool name: "add_chapters".
    /// </summary>
    public class AddChaptersAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "add_chapters";

        public AddChaptersAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger)
            : base(ddrManager, adminLogger)
        {
        }

        public const string ToolUsageMetadata =
    "Adds a single chapter to the DDR with the given title and summary. Used when growing a DDR outline incrementally.";

        public const string ToolSummary = "add multiple chapters to an existing ddr";

        public override string Name => ToolName;

        protected override string Tag => "[AddChaptersAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Add multiple chapters to an existing DDR in a single operation.",
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
                        chapters = new
                        {
                            type = "array",
                            description = "Array of chapters to create in order.",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    title = new
                                    {
                                        type = "string",
                                        description = "Chapter title."
                                    },
                                    summary = new
                                    {
                                        type = "string",
                                        description = "Short chapter summary at the 50K-foot level."
                                    }
                                },
                                required = new[] { "title", "summary" }
                            }
                        }
                    },
                    required = new[] { "identifier", "chapters" }
                }
            };

            return schema;
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(
            JObject payload,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken)
        {
            const string baseTag = "[AddChaptersAgentTool__Execute]";

            var identifier = payload.Value<string>("identifier")?.Trim();
            var chaptersToken = payload["chapters"] as JArray;

            if (string.IsNullOrWhiteSpace(identifier))
            {
                return FromError("identifier is required.");
            }

            if (chaptersToken == null || !chaptersToken.Any())
            {
                return FromError("chapters array is required and must be non-empty.");
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

                var created = new List<JObject>();

                foreach (var item in chaptersToken.OfType<JObject>())
                {
                    var title = item.Value<string>("title")?.Trim();
                    var summary = item.Value<string>("summary")?.Trim();

                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(summary))
                    {
                        return FromError("Each chapter must have title and summary.");
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

                    created.Add(new JObject
                    {
                        ["chapter_id"] = chapter.Id,
                        ["title"] = chapter.Title
                    });
                }

                await _ddrManager.UpdateDdrAsync(ddr, context.Org, context.User);

                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["identifier"] = identifier,
                        ["chapters"] = new JArray(created)
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
