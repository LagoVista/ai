using System;
using System.Collections.Generic;
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
    /// Agent tool that updates the DDR status.
    /// Tool name: "set_ddr_status".
    /// </summary>
    public class SetDdrStatusAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "set_ddr_status";

        public SetDdrStatusAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger)
            : base(ddrManager, adminLogger)
        {
        }

        public const string ToolUsageMetadata =
    "Updates the DDR’s lifecycle status (Draft, InProgress, ReadyForApproval, Approved, Rejected, Tabled). Use to reflect progress or workflow transitions.";


        public override string Name => ToolName;

        public const string ToolSummary = "set status of a ddr";


        protected override string Tag => "[SetDdrStatusAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Set the workflow status for a DDR (e.g. Draft, InProgress, ReadyForApproval, Approved, Rejected, ResearchDraft).",
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
                        status = new
                        {
                            type = "string",
                            description = "New DDR status. Allowed values: Draft, InProgress, ReadyForApproval, Approved, Rejected, ResearchDraft. Case-insensitive."
                        }
                    },
                    required = new[] { "identifier", "status" }
                }
            };

            return schema;
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(
            JObject payload,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken)
        {
            const string baseTag = "[SetDdrStatusAgentTool__Execute]";

            var identifier = payload.Value<string>("identifier")?.Trim();
            var status = payload.Value<string>("status")?.Trim();

            if (string.IsNullOrWhiteSpace(identifier))
            {
                return FromError("identifier is required.");
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                return FromError("status is required.");
            }

            var allowed = new List<string>
            {
                "Draft",
                "InProgress",
                "ReadyForApproval",
                "Approved",
                "Rejected",
                "Tabled",
                "ResearchDraft"
            };

            var canonical = allowed.FirstOrDefault(
                s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));

            if (canonical == null)
            {
                return FromError($"Invalid status '{status}'.");
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

                ddr.Status = canonical;
                ddr.StatusTimestamp = DateTime.UtcNow.ToJSONString();

                await _ddrManager.UpdateDdrAsync(ddr, context.Org, context.User);

                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["identifier"] = identifier,
                        ["status"] = canonical
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
