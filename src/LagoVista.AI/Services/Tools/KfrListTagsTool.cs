using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Returns a unique list of tags across KFR entries in the current session branch.
    /// </summary>
    public sealed class KfrListTagsTool : SessionKfrToolBase
    {
        public const string ToolName = "session_kfr_list_tags";
        public const string ToolSummary = "KFR: list unique tags";

        public const string ToolUsageMetadata = @"KFR: List Tags

Purpose:
Return a unique list of tags across KFR entries in the current session branch.

Input:
- (none)

Output:
- JSON array of strings (sorted)
";

        public override string Name => ToolName;

        public KfrListTagsTool(IAdminLogger logger, IAgentSessionManager sessions)
            : base(logger, sessions)
        {
        }

        public override Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (context?.Session == null)
                return Fail($"{ToolName} requires a valid execution context.");

            try
            {
                var branchList = GetBranchList(context);

                var tags = branchList
                    .Where(k => k?.Tags != null)
                    .SelectMany(k => k.Tags)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return Task.FromResult(InvokeResult<string>.Create(JsonConvert.SerializeObject(tags)));
            }
            catch (Exception ex)
            {
                Logger.AddException("[KfrListTagsTool_ExecuteAsync__Exception]", ex);
                return Fail($"{ToolName} failed to process request.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Return a unique list of tags across KFR entries in the current session branch.",
                p => { });
        }
    }
}
