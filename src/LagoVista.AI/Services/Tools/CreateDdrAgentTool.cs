using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.AspNetCore.Http.Features;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Agent tool that creates a new DDR and allocates the next TLA index.
    /// Tool name: "create_ddr".
    /// </summary>
    public class CreateDdrAgentTool : DdrAgentToolBase
    {
        public const string ToolName = "create_ddr";
        public CreateDdrAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger) : base(ddrManager, adminLogger)
        {
        }

        public const string ToolSummary = "saves a new ddr";
        public const string ToolUsageMetadata = "Creates a new Detailed Design Review (DDR) with the next available numeric index for the specified TLA. When creating this DDR from an imported document it is OK to use the existing index, we don't have to pull from TLA catalog.  Used only when beginning a new DDR document.";
        public override string Name => ToolName;
        protected override string Tag => "[CreateDdrAgentTool]";

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Create a new Detailed Design Review (DDR) for an existing TLA and allocate the next sequence number.", p =>
            {
                p.String("tla", "Three-letter acronym (TLA) such as 'SYS', 'AGN', 'TUL'. Must already exist in the TLA catalog.", required: true);
                p.String("index", "The numeric portion of the identifier, it is found after TLA- and is optional, if it is there, we should use it and not generate a new one.");
                p.String("title", "Short human-readable DDR title (used as the DDR display name).", required: true);
                p.String("summary", "Brief description of the DDR goal and scope (one or two sentences).", required: true);
            });
        }

        protected override async Task<InvokeResult<string>> ExecuteCoreAsync(JObject payload, AgentToolExecutionContext context, CancellationToken cancellationToken)
        {
            const string baseTag = "[CreateDdrAgentTool__Execute]";
            var tla = payload.Value<string>("tla")?.Trim();
            var title = payload.Value<string>("title")?.Trim();
            var summary = payload.Value<string>("summary")?.Trim();
            var strIndex = payload.Value<string>("index")?.Trim();
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
                var tlaIndex = -1;
                var catalog = await _ddrManager.GetTlaCatalogAsync(context.Org, context.User) ?? new List<DdrTla>();
                if (!catalog.Any(t => string.Equals(t.Tla, tla, StringComparison.OrdinalIgnoreCase)))
                {
                    return FromError($"Unknown TLA '{tla}', available TLAs are [{String.Join(',', catalog.Select(d => d.Tla))}].");
                }

                if (String.IsNullOrEmpty(strIndex) || !int.TryParse(strIndex, out tlaIndex))
                {
                    var indexResult = await _ddrManager.AllocateTlaIndex(tla, context.Org, context.User);
                    if (!indexResult.Successful)
                    {
                        _adminLogger.LogInvokeResult(baseTag, indexResult);
                        return InvokeResult<string>.FromInvokeResult(indexResult.ToInvokeResult());
                    }

                    tlaIndex = indexResult.Result;
                }

                var identifier = $"{tla}-{tlaIndex:D3}";
                var now = DateTime.UtcNow.ToJSONString();
                var ddr = new DetailedDesignReview
                {
                    Tla = tla,
                    Index = tlaIndex,
                    Name = title,
                    DdrIdentifier = identifier,
                    Key = identifier.Replace("-", "").ToLower(),
                    Description = summary,
                    Status = "Draft",
                    StatusTimestamp = now,
                    Goal = null,
                    CreationDate = now,
                    LastUpdatedDate = now,
                    CreatedBy = context.User,
                    LastUpdatedBy = context.User,
                    OwnerOrganization = context.Org,
                    GoalApprovedBy = null,
                    GoalApprovedTimestamp = null,
                    ApprovedBy = null,
                    ApprovedTimestamp = null,
                    Chapters = new List<DdrChapter>()
                };
                var result = Validator.Validate(ddr);
                if (!result.Successful)
                {
                    return InvokeResult<string>.FromInvokeResult(result.ToInvokeResult());
                }

                var addResult = await _ddrManager.AddDdrAsync(ddr, context.Org, context.User);
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
                        ["identifier"] = identifier,
                        ["tla"] = tla,
                        ["title"] = title,
                        ["summary"] = summary,
                        ["status"] = ddr.Status
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