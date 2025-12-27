using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Agent tool that marks a DDR as fully approved after its goal has been approved.
    /// Tool name: "approve_ddr".
    /// </summary>
    public class ApproveDdrAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "approve_ddr";
        public ApproveDdrAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger) : base(ddrManager, adminLogger)
        {
        }

        public const string ToolUsageMetadata = "Approves the entire DDR after its goal and chapters are approved. Finalizes the DDR. Requires explicit user approval.";
        public const string ToolSummary = "approve a ddr";
        public override string Name => ToolName;
        protected override string Tag => "[ApproveDdrAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Approve a DDR after its goal has been approved, recording approver and timestamps.", p =>
            {
                p.String("identifier", "DDR identifier in TLA-### format, for example 'SYS-001'.", required: true);
            });
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(JObject payload, AgentToolExecutionContext context, CancellationToken cancellationToken)
        {
            const string baseTag = "[ApproveDdrAgentTool__Execute]";
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

                if (string.IsNullOrWhiteSpace(ddr.Goal) || string.IsNullOrWhiteSpace(ddr.GoalApprovedTimestamp))
                {
                    return FromError("DDR cannot be approved because the goal has not been approved.");
                }

                var now = DateTime.UtcNow.ToJSONString();
                ddr.ApprovedBy = context.User;
                ddr.ApprovedTimestamp = now;
                ddr.Status = "Approved";
                ddr.StatusTimestamp = now;
                await _ddrManager.UpdateDdrAsync(ddr, context.Org, context.User);
                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["identifier"] = identifier,
                        ["status"] = ddr.Status,
                        ["approved_by"] = new JObject
                        {
                            ["id"] = context.User.Id,
                            ["text"] = context.User.Text
                        },
                        ["approved_timestamp"] = now
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