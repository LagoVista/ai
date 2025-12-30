using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Evict (deactivate) one or more KFR entries by id in the current session branch.
    /// </summary>
    public sealed class SessionKfrEvictTool : SessionKfrToolBase
    {
        public const string ToolName = "session_kfr_evict";
        public const string ToolSummary = "KFR: evict (deactivate) entries by id";

        public const string ToolUsageMetadata = @"Tool Usage Instructions — KFR (Evict)

Purpose:
Remove operationally-unneeded KFR entries by deactivating them.

When to Call:
- When existing KFR content is no longer required for near-term correctness.
- When you need to dismiss entries that were previously added.

Rules:
- Provide one or more kfrIds.
- If a matched entry has requiresResolution=true, eviction is refused unless force=true.
- Eviction is soft: entries become inactive.
";

        public override string Name => ToolName;

        public SessionKfrEvictTool(IAdminLogger logger, IAgentSessionManager sessions)
            : base(logger, sessions)
        {
        }

        private sealed class Args
        {
            public List<string> KfrIds { get; set; }
            public bool? Force { get; set; }
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

                var ids = args.KfrIds?
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();

                if (ids.Count == 0)
                    return Fail($"{ToolName} requires 'kfrIds' (one or more KFR ids).");

                var force = args.Force.HasValue && args.Force.Value;

                var branchList = GetBranchList(context);

                var matches = branchList
                    .Where(k => k.IsActive && ids.Contains(k.KfrId, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count == 0)
                    return Task.FromResult(Ok("evict", context, new List<AgentSessionKfrEntry>()));

                var blocked = matches.Where(m => m.RequiresResolution).ToList();
                if (blocked.Count > 0 && !force)
                {
                    var blockedIds = string.Join(", ", blocked.Select(b => b.KfrId));
                    return Fail($"{ToolName} refused: the following KFR entries require resolution: {blockedIds}. Set force=true to evict anyway.");
                }

                var stamp = DateTime.UtcNow.ToString("o");
                foreach (var m in matches)
                {
                    m.IsActive = false;
                    m.LastUpdatedDate = stamp;
                }

                return Task.FromResult(Ok("evict", context, matches));
            }
            catch (Exception ex)
            {
                Logger.AddException("[SessionKfrEvictTool_ExecuteAsync__Exception]", ex);
                return Fail($"{ToolName} failed to process request.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Evict (deactivate) one or more KFR entries by id in the current session branch.",
                p =>
                {
                    p.StringArray("kfrIds", "KFR ids to evict.");
                    p.Boolean("force", "If true, allow evicting entries that require resolution.", required: false);
                });
        }
    }
}
