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
    /// Agent tool that retrieves full DDR details, including chapters.
    /// Tool name: "get_ddr".
    /// </summary>
    public class GetDdrAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "get_ddr";

        public GetDdrAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger)
            : base(ddrManager, adminLogger)
        {
        }
        public const string ToolUsageMetadata =
    @"Retrieves the full DDR, including metadata, chapters, and approval state. Used for review, summarization, or downstream analysis.  
      In other cases the ddr will be provided in content";

        public override string Name => ToolName;

        protected override string Tag => "[GetDdrAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Get a full DDR snapshot including metadata, goal, approval info, and all chapters.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        identifier = new
                        {
                            type = "string",
                            description = "DDR identifier in TLA-### format, for example 'SYS-001'."
                        }
                    },
                    required = new[] { "identifier" }
                }
            };

            return schema;
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(
            JObject payload,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken)
        {
            const string baseTag = "[GetDdrAgentTool__Execute]";

            var identifier = payload.Value<string>("identifier")?.Trim();
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return FromError("identifier is required.");
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

                var chapters = ddr.Chapters ?? new List<DdrChapter>();

                var chapterArray = new JArray(
                    chapters.Select(c => new JObject
                    {
                        ["id"] = c.Id,
                        ["title"] = c.Title,
                        ["summary"] = c.Summary,
                        ["details"] = c.Details,
                        ["approved_by"] = c.ApprovedBy == null
                            ? null
                            : new JObject
                            {
                                ["id"] = c.ApprovedBy.Id,
                                ["text"] = c.ApprovedBy.Text
                            },
                        ["approved_timestamp"] = c.ApprovedTimestamp
                    }));

                var resultBody = new JObject
                {
                    ["identifier"] = $"{ddr.Tla}-{ddr.Index:D3}",
                    ["tla"] = ddr.Tla,
                    ["title"] = ddr.Name,
                    ["summary"] = ddr.Description,
                    ["status"] = ddr.Status,
                    ["status_timestamp"] = ddr.StatusTimestamp,
                    ["goal"] = ddr.Goal,
                    ["goal_approved_by"] = ddr.GoalApprovedBy == null
                        ? null
                        : new JObject
                        {
                            ["id"] = ddr.GoalApprovedBy.Id,
                            ["text"] = ddr.GoalApprovedBy.Text
                        },
                    ["goal_approved_timestamp"] = ddr.GoalApprovedTimestamp,
                    ["approved_by"] = ddr.ApprovedBy == null
                        ? null
                        : new JObject
                        {
                            ["id"] = ddr.ApprovedBy.Id,
                            ["text"] = ddr.ApprovedBy.Text
                        },
                    ["approved_timestamp"] = ddr.ApprovedTimestamp,
                    ["content"] = ddr.FullDDRMarkDown,
                    ["chapters"] = chapterArray
                };

                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = resultBody
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
