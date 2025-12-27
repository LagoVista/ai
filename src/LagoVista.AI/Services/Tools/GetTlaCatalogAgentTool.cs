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
    /// Agent tool that returns the DDR TLA catalog.
    /// Tool name: "get_tla_catalog".
    /// </summary>
    public class GetTlaCatalogAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "get_tla_catalog";

        public GetTlaCatalogAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger)
            : base(ddrManager, adminLogger)
        {
        }

        public const string ToolUsageMetadata =
     "Retrieves the full catalog of DDR TLAs, including titles and summaries. Used when the LLM needs to browse existing domains before creating or organizing DDRs.";

        public const string ToolSummary = "get three letter acroynm (TLA) catalog";

        public override string Name => ToolName;

        protected override string Tag => "[GetTlaCatalogAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "List all registered DDR TLAs and their titles/summaries.",
                parameters = new
                {
                    type = "object",
                    properties = new { },
                    required = System.Array.Empty<string>()
                }
            };

            return schema;
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(
            JObject payload,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken)
        {
            const string baseTag = "[GetTlaCatalogAgentTool__Execute]";

            try
            {
                var catalog = await _ddrManager.GetTlaCatalogAsync(context.Org, context.User)
                              ?? new List<DdrTla>();

                var tlasArray = new JArray(
                    catalog.Select(t => new JObject
                    {
                        ["tla"] = t.Tla,
                        ["title"] = t.Title,
                        ["summary"] = t.Summary
                    }));

                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["tlas"] = tlasArray
                    }
                };

                return FromEnvelope(envelope);
            }
            catch (Exception ex)
            {
                _ddrManager?.ToString(); // keep analyzer quiet if needed
                _adminLogger.AddException(baseTag, ex);
                return InvokeResult<string>.FromException(baseTag, ex);
            }
        }
    }
}
