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
    /// Agent tool that reorders chapters for a DDR.
    /// Tool name: "reorder_chapters".
    /// </summary>
    public class ReorderChaptersAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "reorder_chapters";

        public ReorderChaptersAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger)
            : base(ddrManager, adminLogger)
        {
        }

        public const string ToolSummary = "reorder the chapters in a ddr";


        public override string Name => ToolName;

        public const string ToolUsageMetadata =
    "Changes the chapter ordering within a DDR. Use when restructuring the narrative or improving document flow.";


        protected override string Tag => "[ReorderChaptersAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Reorder all chapters for a DDR. The provided chapter_ids must match the existing chapter IDs exactly.",
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
                        chapter_ids = new
                        {
                            type = "array",
                            description = "New ordered list of chapter IDs. Must contain exactly the same IDs as the current chapters.",
                            items = new
                            {
                                type = "string"
                            }
                        }
                    },
                    required = new[] { "identifier", "chapter_ids" }
                }
            };

            return schema;
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(
            JObject payload,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken)
        {
            const string baseTag = "[ReorderChaptersAgentTool__Execute]";

            var identifier = payload.Value<string>("identifier")?.Trim();
            var idsArray = payload["chapter_ids"] as JArray;

            if (string.IsNullOrWhiteSpace(identifier))
            {
                return FromError("identifier is required.");
            }

            if (idsArray == null || !idsArray.Any())
            {
                return FromError("chapter_ids must be a non-empty array.");
            }

            var newOrderIds = idsArray.Values<string>().ToList();

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

                var chapters = ddr.Chapters ?? new List<DdrChapter>();

                var existingIds = new HashSet<string>(chapters.Select(c => c.Id));
                var requestedIds = new HashSet<string>(newOrderIds);

                if (!existingIds.SetEquals(requestedIds))
                {
                    return FromError("chapter_ids must match existing chapter IDs exactly.");
                }

                var reordered = new List<DdrChapter>();
                foreach (var cid in newOrderIds)
                {
                    var chapter = chapters.First(c => c.Id == cid);
                    reordered.Add(chapter);
                }

                ddr.Chapters = reordered;

                await _ddrManager.UpdateDdrAsync(ddr, context.Org, context.User);

                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["identifier"] = identifier,
                        ["chapter_ids"] = new JArray(newOrderIds)
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
