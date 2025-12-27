using System;
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
    /// Agent tool that sets or updates the goal text on a DDR prior to approval.
    /// Tool name: "set_goal".
    /// </summary>
    public class SetGoalAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "set_goal";

        public SetGoalAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger)
            : base(ddrManager, adminLogger)
        {
        }

        public const string ToolUsageMetadata =
    "Sets or revises the goal statement for a DDR. Used early in the DDR lifecycle before the goal has been approved.";

        public override string Name => ToolName;

        public const string ToolSummary = "set the goal of a ddr";

        protected override string Tag => "[SetGoalAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Set or update the goal statement for a DDR before it is approved.",
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
                        goal = new
                        {
                            type = "string",
                            description = "Concise statement of the DDR goal and what success looks like."
                        }
                    },
                    required = new[] { "identifier", "goal" }
                }
            };

            return schema;
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(
            JObject payload,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken)
        {
            const string baseTag = "[SetGoalAgentTool__Execute]";

            var identifier = payload.Value<string>("identifier")?.Trim();
            var goal = payload.Value<string>("goal")?.Trim();

            if (string.IsNullOrWhiteSpace(identifier))
            {
                return FromError("identifier is required.");
            }

            if (string.IsNullOrWhiteSpace(goal))
            {
                return FromError("goal is required.");
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

                if (!string.IsNullOrWhiteSpace(ddr.GoalApprovedTimestamp))
                {
                    return FromError("Goal is already approved and cannot be modified.");
                }

                ddr.Goal = goal;

                await _ddrManager.UpdateDdrAsync(ddr, context.Org, context.User);

                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["identifier"] = identifier,
                        ["goal"] = goal,
                        ["is_approved"] = false
                    }
                };

                return FromEnvelope(envelope);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                _adminLogger.AddException(baseTag, ex);
                return InvokeResult<string>.FromException(baseTag, ex);
            }
        }
    }
}
