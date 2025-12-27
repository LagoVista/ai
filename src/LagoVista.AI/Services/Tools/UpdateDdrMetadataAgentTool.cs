using System;
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
    /// Agent tool that updates DDR title/summary metadata.
    /// Tool name: "update_ddr_metadata".
    /// </summary>
    public class UpdateDdrMetadataAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "update_ddr_metadata";
        public UpdateDdrMetadataAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger) : base(ddrManager, adminLogger)
        {
        }

        public const string ToolUsageMetadata = "Updates the DDR's human-readable title or summary metadata. Should be used when refining or correcting top-level DDR information.";
        public const string ToolSummary = "udpate the title, and summary within a ddr";
        public override string Name => ToolName;
        protected override string Tag => "[UpdateDdrMetadataAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Update the DDR title and/or summary metadata.", p =>
            {
                p.String("identifier", "DDR identifier in TLA-### format, for example 'SYS-001'.", required: true);
                p.String("jsonl", "JSONL Summary of the DDR that will be used to pass as an artifact to LLM to establish context.");
                p.String("title", "Optional new DDR title. If omitted, the title is not changed.");
                p.String("summary", "Optional new DDR summary/description. If omitted, the summary is not changed.");
                p.String("modeInstructions", "Optional new DDR mode instructions. If omitted, the mode instructions are not changed.");
                p.String("notes", "Optional note containing any relevant information such as warnings.");
            });
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(JObject payload, AgentToolExecutionContext context, CancellationToken cancellationToken)
        {
            const string baseTag = "[UpdateDdrMetadataAgentTool__Execute]";
            var identifier = payload.Value<string>("identifier")?.Trim();
            var title = payload.Value<string>("title")?.Trim();
            var summary = payload.Value<string>("summary")?.Trim();
            var notes = payload.Value<string>("notes")?.Trim();
            var modeInstructions = payload.Value<string>("modeInstructions")?.Trim();
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return FromError("identifier is required.");
            }

            if (title == null && summary == null)
            {
                return FromError("At least one of title or summary must be provided.");
            }

            try
            {
                var ddr = await _ddrManager.GetDdrByTlaIdentiferAsync(identifier, context.Org, context.User);
                if (ddr == null)
                {
                    return FromError($"DDR '{identifier}' not found.");
                }

                if (!string.IsNullOrWhiteSpace(title))
                {
                    ddr.Name = title;
                }

                if (!string.IsNullOrWhiteSpace(summary))
                {
                    ddr.HumanSummary = summary;
                    ddr.Description = summary;
                }

                if (!string.IsNullOrWhiteSpace(notes))
                {
                    ddr.Notes = notes;
                }

                if (!string.IsNullOrWhiteSpace(modeInstructions))
                {
                    ddr.AgentInstructions = modeInstructions;
                }

                await _ddrManager.UpdateDdrAsync(ddr, context.Org, context.User);
                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["identifier"] = identifier,
                        ["title"] = ddr.Name,
                        ["summary"] = ddr.Description,
                        ["notes"] = ddr.Notes,
                        ["jsonl"] = ddr.AgentInstructions
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