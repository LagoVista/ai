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
    /// Agent tool that adds a new TLA to the DDR catalog.
    /// Tool name: "add_tla".
    /// </summary>
    public class AddTlaAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "add_tla";

        public AddTlaAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger)
            : base(ddrManager, adminLogger)
        {
        }

        public const string ToolUsageMetadata =
    "Adds a new TLA domain to the DDR catalog. Should be used only when introducing a new domain for DDRs. Requires explicit user intent before use.";


        public override string Name => ToolName;

        protected override string Tag => "[AddTlaAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Add a new TLA (three-letter acronym) to the DDR catalog.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        tla = new
                        {
                            type = "string",
                            description = "Three-letter acronym such as 'SYS', 'AGN', or 'TUL'. Will be uppercased."
                        },
                        title = new
                        {
                            type = "string",
                            description = "Human-friendly title for this TLA domain."
                        },
                        summary = new
                        {
                            type = "string",
                            description = "Short description of the domain covered by this TLA."
                        }
                    },
                    required = new[] { "tla", "title", "summary" }
                }
            };

            return schema;
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(
            JObject payload,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken)
        {
            const string baseTag = "[AddTlaAgentTool__Execute]";

            var tla = payload.Value<string>("tla")?.Trim();
            var title = payload.Value<string>("title")?.Trim();
            var summary = payload.Value<string>("summary")?.Trim();

            if (string.IsNullOrWhiteSpace(tla))
            {
                return FromError("tla is required.");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return FromError("title is required.");
            }

            if (string.IsNullOrWhiteSpace(summary))
            {
                return FromError("summary is required.");
            }

            tla = tla.ToUpperInvariant();

            try
            {
                var existing = await _ddrManager.GetTlaCatalogAsync(context.Org, context.User)
                               ?? new List<DdrTla>();

                if (existing.Any(t => string.Equals(t.Tla, tla, StringComparison.OrdinalIgnoreCase)))
                {
                    return FromError($"TLA '{tla}' already exists.");
                }

                var newTla = new DdrTla
                {
                    Tla = tla,
                    Title = title,
                    Summary = summary,
                    CurrentIndex = 0
                };

                var addResult = await _ddrManager.AddTlaCatalog(newTla, context.Org, context.User);
                if (!addResult.Successful)
                {
                    _adminLogger.LogInvokeResult(baseTag, addResult);
                    return InvokeResult<string>.FromInvokeResult(addResult);
                }

                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["tla"] = tla,
                        ["title"] = title,
                        ["summary"] = summary
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
