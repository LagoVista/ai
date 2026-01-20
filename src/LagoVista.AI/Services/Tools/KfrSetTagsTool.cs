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
    /// Replace tags on an existing KFR entry in the current session branch.
    /// </summary>
    public sealed class KfrSetTagsTool : SessionKfrToolBase
    {
        public const string ToolName = "session_kfr_set_tags";
        public const string ToolSummary = "KFR: set tags on an entry";

        public const string ToolUsageMetadata = @"KFR: Set Tags

Purpose:
Replace the tags list on an existing KFR entry in the current session branch.

Input:
- kfrId (string, required): target entry id (e.g., KFR-GOAL-001)
- tags (string[], optional): full replacement list; omit or [] clears tags
";

        public override string Name => ToolName;

        public KfrSetTagsTool(IAdminLogger logger, IAgentSessionManager sessions)
            : base(logger, sessions)
        {
        }

        private sealed class Args
        {
            public string KfrId { get; set; }
            public List<string> Tags { get; set; }
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

                if (string.IsNullOrWhiteSpace(args.KfrId))
                    return Fail($"{ToolName} requires 'kfrId'.");

                var branchList = GetBranchList(context);
                var entry = branchList.SingleOrDefault(k => k.KfrId.Equals(args.KfrId.Trim(), StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                    return Fail($"{ToolName} could not find KFR entry '{args.KfrId}'.");

                entry.Tags = (args.Tags ?? new List<string>())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                entry.LastUpdatedDate = UtcStamp();

                return Task.FromResult(Ok("setTags", context, new List<AgentSessionKfrEntry> { entry }));
            }
            catch (Exception ex)
            {
                Logger.AddException("[KfrSetTagsTool_ExecuteAsync__Exception]", ex);
                return Fail($"{ToolName} failed to process request.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Replace tags on an existing KFR entry in the current session branch.",
                p =>
                {
                    p.String("kfrId", "Target KFR entry id.", required: true);
                    p.StringArray("tags", "Replacement tags list. Omit or [] clears tags.");
                });
        }
    }
}
