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
    /// Agent tool that moves a DDR from one TLA domain to another, allocating a new index.
    /// Tool name: "move_ddr_tla".
    /// </summary>
    public class MoveDdrTlaAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "move_ddr_tla";
        public MoveDdrTlaAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger) : base(ddrManager, adminLogger)
        {
        }

        public const string ToolUsageMetadata = "Moves a DDR from one TLA domain to another, assigning a new index. Use only when the DDR was misclassified or organizational boundaries change.";
        public const string ToolSummary = "use to reassign tla id on a ddr";
        public override string Name => ToolName;
        protected override string Tag => "[MoveDdrTlaAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Move a DDR to a new TLA domain, allocating a new index and identifier.", p =>
            {
                p.String("identifier", "Existing DDR identifier in TLA-### format, for example 'SYS-001'.", required: true);
                p.String("new_tla", "Target TLA to move the DDR into, e.g. 'AGN' or 'TUL'. Must already exist in the TLA catalog.", required: true);
            });
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(JObject payload, AgentToolExecutionContext context, CancellationToken cancellationToken)
        {
            const string baseTag = "[MoveDdrTlaAgentTool__Execute]";
            var identifier = payload.Value<string>("identifier")?.Trim();
            var newTla = payload.Value<string>("new_tla")?.Trim();
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return FromError("identifier is required.");
            }

            if (string.IsNullOrWhiteSpace(newTla))
            {
                return FromError("new_tla is required.");
            }

            newTla = newTla.ToUpperInvariant();
            try
            {
                var catalog = await _ddrManager.GetTlaCatalogAsync(context.Org, context.User) ?? new List<DdrTla>();
                if (!catalog.Any(t => string.Equals(t.Tla, newTla, StringComparison.OrdinalIgnoreCase)))
                {
                    return FromError($"Unknown TLA '{newTla}'.");
                }

                var ddr = await _ddrManager.GetDdrByTlaIdentiferAsync(identifier, context.Org, context.User);
                if (ddr == null)
                {
                    return FromError($"DDR '{identifier}' not found.");
                }

                var oldIdentifier = $"{ddr.Tla}-{ddr.Index:D3}";
                if (string.Equals(ddr.Tla, newTla, StringComparison.OrdinalIgnoreCase))
                {
                    var noOpEnvelope = new JObject
                    {
                        ["ok"] = true,
                        ["result"] = new JObject
                        {
                            ["old_identifier"] = oldIdentifier,
                            ["new_identifier"] = oldIdentifier,
                            ["tla"] = ddr.Tla
                        }
                    };
                    return FromEnvelope(noOpEnvelope);
                }

                var newIndexResult = await _ddrManager.AllocateTlaIndex(newTla, context.Org, context.User);
                if (!newIndexResult.Successful)
                {
                    _adminLogger.LogInvokeResult(baseTag, newIndexResult);
                    return InvokeResult<string>.FromInvokeResult(newIndexResult.ToInvokeResult());
                }

                var newIndex = newIndexResult.Result;
                ddr.Tla = newTla;
                ddr.Index = newIndex;
                await _ddrManager.UpdateDdrAsync(ddr, context.Org, context.User);
                var newIdentifier = $"{newTla}-{newIndex:D3}";
                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["old_identifier"] = oldIdentifier,
                        ["new_identifier"] = newIdentifier,
                        ["tla"] = newTla
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