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
    /// Set category on an existing KFR entry in the current session branch.
    /// </summary>
    public sealed class KfrSetCategoryTool : SessionKfrToolBase
    {
        public const string ToolName = "kfr_set_category";
        public const string ToolSummary = "KFR: set category on an entry";

        public const string ToolUsageMetadata = @"KFR: Set Category

Purpose:
Set the category on an existing KFR entry in the current session branch.

Input:
- kfrId (string, required): target entry id (e.g., KFR-GOAL-001)
- category (string, optional): category value; omit or empty clears category
";

        public override string Name => ToolName;

        public KfrSetCategoryTool(IAdminLogger logger, IAgentSessionManager sessions)
            : base(logger, sessions)
        {
        }

        private sealed class Args
        {
            public string KfrId { get; set; }
            public string Category { get; set; }
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

                entry.Category = string.IsNullOrWhiteSpace(args.Category) ? null : args.Category.Trim();
                entry.LastUpdatedDate = UtcStamp();

                return Task.FromResult(Ok("setCategory", context,new List<AgentSessionKfrEntry> { entry }));
            }
            catch (Exception ex)
            {
                Logger.AddException("[KfrSetCategoryTool_ExecuteAsync__Exception]", ex);
                return Fail($"{ToolName} failed to process request.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Set category on an existing KFR entry in the current session branch.",
                p =>
                {
                    p.String("kfrId", "Target KFR entry id.", required: true);
                    p.String("category", "Category value. Omit or empty clears category.");
                });
        }
    }
}
