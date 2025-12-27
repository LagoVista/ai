using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Agent tool that lists all DDRs for the current org.
    /// Tool name: "list_ddrs".
    /// </summary>
    public class ListDdrsAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "list_ddrs";

        public ListDdrsAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger)
            : base(ddrManager, adminLogger)
        {
        }

        public const string ToolUsageMetadata =
    "Lists all DDRs in the system along with basic metadata. Used for navigation, search, and overview operations.";

        public override string Name => ToolName;

        public const string ToolSummary = "list all ddrs";

        protected override string Tag => "[ListDdrsAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = @"List all DDRs for the current organization, returning identifier, title, status, and status timestamp on the first line.  '
                                Pleae make sure the first column is wide enough for 10 characters. You should return the status time stamp as-is, with a column label Status Date.  
                                On the second row for each DDR you should return the summary if it's available.  If not lust leave the line off.",
                parameters = new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                }
            };

            return schema;
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(
            JObject payload,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken)
        {
            const string baseTag = "[ListDdrsAgentTool__Execute]";

            try
            {
                var req = ListRequest.Create();

                var response = await _ddrManager.GetDdrsAsync(context.Org, context.User, req);
                if (response == null)
                {
                    return FromError("Unexpected null response from GetDdrsAsync.");
                }

                var items = new JArray(
                    response.Model.Select(m => new JObject
                    {
                        ["identifier"] = m.DdrIdentifier,
                        ["title"] = m.Name,
                        ["summary"] = String.IsNullOrEmpty(m.Summary) ? m.Description : m.Summary,
                        ["status"] = m.Status,
                        ["status_timestamp"] = m.StatusTimestamp
                    }));

                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = new JObject
                    {
                        ["items"] = items,
                        ["record_count"] = response.RecordCount
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
