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
    /// Returns a unique list of categories across KFR entries in the current session branch.
    /// </summary>
    public sealed class KfrListCategoriesTool : SessionKfrToolBase
    {
        public const string ToolName = "session_kfr_list_categories";
        public const string ToolSummary = "KFR: list unique categories";

        public const string ToolUsageMetadata = @"KFR: List Categories

Purpose:
Return a unique list of category values across KFR entries in the current session branch.

Input:
- (none)

Output:
- JSON array of strings (sorted)
";

        public override string Name => ToolName;

        public KfrListCategoriesTool(IAdminLogger logger, IAgentSessionManager sessions)
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

                var categories = branchList
                    .Where(k => k != null && !string.IsNullOrWhiteSpace(k.Category))
                    .Select(k => k.Category.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return Task.FromResult(InvokeResult<string>.Create(JsonConvert.SerializeObject(categories)));
            }
            catch (Exception ex)
            {
                Logger.AddException("[KfrListCategoriesTool_ExecuteAsync__Exception]", ex);
                return Fail($"{ToolName} failed to process request.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Return a unique list of category values across KFR entries in the current session branch.",
                p => { });
        }
    }
}
