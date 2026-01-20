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
    /// Query KFR entries by tags.
    /// Defaults to active-only; can include inactive entries.
    /// </summary>
    public sealed class KfrQueryByTagsTool : SessionKfrToolBase
    {
        public const string ToolName = "kfr_query_by_tags";
        public const string ToolSummary = "KFR: query entries by tags";

        public const string ToolUsageMetadata = @"KFR: Query By Tags

Purpose:
Return KFR entries that match one or more tags.

Input:
- tags (string[], required; 1+)
- matchMode (string, optional): 'any' (default) or 'all'
- includeInactive (boolean, optional; default false)

Notes:
- Matching is case-insensitive.
";

        public override string Name => ToolName;

        public KfrQueryByTagsTool(IAdminLogger logger, IAgentSessionManager sessions)
            : base(logger, sessions)
        {
        }

        private sealed class Args
        {
            public List<string> Tags { get; set; }
            public string MatchMode { get; set; } // any | all
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

                if (args.Tags == null || args.Tags.Count == 0)
                    return Fail($"{ToolName} requires 'tags' (one or more)." );

                var queryTags = args.Tags
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (queryTags.Count == 0)
                    return Fail($"{ToolName} requires 'tags' (one or more non-empty values)." );

                var mode = string.IsNullOrWhiteSpace(args.MatchMode) ? "any" : args.MatchMode.Trim();
                var matchAll = mode.Equals("all", StringComparison.OrdinalIgnoreCase);
                if (!matchAll && !mode.Equals("any", StringComparison.OrdinalIgnoreCase))
                    return Fail($"{ToolName} invalid matchMode '{args.MatchMode}'. Use 'any' or 'all'.");

                var branchList = GetBranchList(context);

                bool HasTag(AgentSessionKfrEntry entry, string tag)
                {
                    if (entry?.Tags == null) return false;
                    return entry.Tags.Any(t => !string.IsNullOrWhiteSpace(t) && t.Trim().Equals(tag, StringComparison.OrdinalIgnoreCase));
                }

                var matches = branchList
                    .Where(k => k != null)
                    .Where(k => args.IncludeInactive || k.IsActive)
                    .Where(k =>
                    {
                        if (matchAll)
                            return queryTags.All(tag => HasTag(k, tag));

                        return queryTags.Any(tag => HasTag(k, tag));
                    })
                    .ToList();

                return Task.FromResult(Ok("queryByTags", context, matches));
            }
            catch (Exception ex)
            {
                Logger.AddException("[KfrQueryByTagsTool_ExecuteAsync__Exception]", ex);
                return Fail($"{ToolName} failed to process request.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Query KFR entries by tags.",
                p =>
                {
                    p.StringArray("tags", "Tags to match (one or more).");
                    p.String("matchMode", "'any' (default) or 'all'.");
                    p.Boolean("includeInactive", "If true, include inactive entries.");
                });
        }
    }
}
