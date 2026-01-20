using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Query KFR entries by exact category match.
    /// Defaults to active-only; can include inactive entries.
    /// </summary>
    public sealed class KfrQueryByCategoryTool : SessionKfrToolBase
    {
        public const string ToolName = "session_kfr_query_by_category";
        public const string ToolSummary = "KFR: query entries by category";

        public const string ToolUsageMetadata = @"KFR: Query By Category

Purpose:
Return KFR entries whose Category exactly matches the provided category.

Input:
- category (string, required)
- includeInactive (boolean, optional; default false)

Notes:
- Matching is case-insensitive.
";

        public override string Name => ToolName;

        public KfrQueryByCategoryTool(IAdminLogger logger, IAgentSessionManager sessions)
            : base(logger, sessions)
        {
        }

        private sealed class Args
        {
            public string Category { get; set; }
            public bool IncludeInactive { get; set; }
        }

        public override Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (context?.Session == null)
                return Fail($"{ToolName} requires a valid execution context.");

            try
            {
                var args = string.IsNullOrWhiteSpace(argumentsJson)
                    ? new Args()
                    : (JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args());

                if (string.IsNullOrWhiteSpace(args.Category))
                    return Fail($"{ToolName} requires 'category'.");

                var category = args.Category.Trim();
                var branchList = GetBranchList(context);

                var matches = branchList
                    .Where(k => k != null)
                    .Where(k => args.IncludeInactive || k.IsActive)
                    .Where(k => !string.IsNullOrWhiteSpace(k.Category) && k.Category.Trim().Equals(category, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return Task.FromResult(Ok("queryByCategory", context, matches));
            }
            catch (Exception ex)
            {
                Logger.AddException("[KfrQueryByCategoryTool_ExecuteAsync__Exception]", ex);
                return Fail($"{ToolName} failed to process request.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Query KFR entries by exact category match.",
                p =>
                {
                    p.String("category", "Category to match (case-insensitive).", required: true);
                    p.Boolean("includeInactive", "If true, include inactive entries.");
                });
        }
    }
}
