using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Agent tool that approves the goal for a DDR.
    /// Tool name: "approve_goal".
    /// </summary>
    public class ApproveGoalAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "approve_goal";
        public ApproveGoalAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger) : base(ddrManager, adminLogger)
        {
        }

        public const string ToolUsageMetadata = "Approves a DDR's goal statement. This locks the goal and enables downstream DDR work. Requires explicit user approval.";
        public override string Name => ToolName;

        public const string ToolSummary = "approve a goal within a ddr";
        protected override string Tag => "[ApproveGoalAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Approve the goal statement for a DDR, recording approver and timestamp. The goal text must already be set.", p =>
            {
                p.String("identifier", "DDR identifier in TLA-### format, for example 'SYS-001'.", required: true);
            });
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(JObject payload, AgentToolExecutionContext context, CancellationToken cancellationToken)
        {
            const string baseTag = "[ApproveGoalAgentTool__Execute]";
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

                if (string.IsNullOrWhiteSpace(ddr.Goal))
                {
                    return FromError("Cannot approve goal because it is empty.");
                }

                if (ddr.GoalApprovedBy != null && !string.IsNullOrWhiteSpace(ddr.GoalApprovedTimestamp))
                {
                    var existingEnvelope = new JObject
                    {
                        ["ok"] = true,
                        ["result"] = new JObject
                        {
                            ["identifier"] = identifier,
                            ["goal_approved_by"] = new JObject
                            {
                                ["id"] = ddr.GoalApprovedBy.Id,
                                ["text"] = ddr.GoalApprovedBy.Text
                            },
                            ["goal_approved_timestamp"] = ddr.GoalApprovedTimestamp
                        }
                    };
                    return FromEnvelope(existingEnvelope);
                }

                ddr.GoalApprovedBy = context.User;
                ddr.GoalApprovedTimestamp = DateTime.UtcNow.ToJSONString();
                await _ddrManager.UpdateDdrAsync(ddr, context.Org, context.User);
                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["identifier"] = identifier,
                        ["goal_approved_by"] = new JObject
                        {
                            ["id"] = context.User.Id,
                            ["text"] = context.User.Text
                        },
                        ["goal_approved_timestamp"] = ddr.GoalApprovedTimestamp
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