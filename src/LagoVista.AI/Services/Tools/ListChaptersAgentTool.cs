using System;
using System.Collections.Generic;
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
    /// Agent tool that lists chapters for a DDR.
    /// Tool name: "list_chapters".
    /// </summary>
    public class ListChaptersAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "list_chapters";
        public const string ToolUsageMetadata = "Retrieves the list of chapters for a DDR, including approval state. Used for navigation, summarization, or planning follow-up work.";
        public ListChaptersAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger) : base(ddrManager, adminLogger)
        {
        }

        public const string ToolSummary = "list chapters in a ddr by identifer";
        public override string Name => ToolName;
        protected override string Tag => "[ListChaptersAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "List all chapters for a DDR, including id, title, summary, and approval flag.", p =>
            {
                p.String("identifier", "DDR identifier in TLA-### format, for example 'SYS-001'.", required: true);
            });
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(JObject payload, AgentToolExecutionContext context, CancellationToken cancellationToken)
        {
            const string baseTag = "[ListChaptersAgentTool__Execute]";
            var identifier = payload.Value<string>("identifier")?.Trim();
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return FromError("identifier is required.");
            }

            try
            {
                var ddr = await _ddrManager.GetDdrByTlaIdentiferAsync(identifier, context.Org, context.User);
                if (ddr == null)
                {
                    return FromError($"DDR '{identifier}' not found.");
                }

                var chapters = ddr.Chapters ?? new List<DdrChapter>();
                var items = new JArray(chapters.Select(c => new JObject { ["id"] = c.Id, ["title"] = c.Title, ["summary"] = c.Summary, ["approved"] = c.ApprovedBy != null && !string.IsNullOrWhiteSpace(c.ApprovedTimestamp) }));
                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["identifier"] = identifier,
                        ["chapters"] = items
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