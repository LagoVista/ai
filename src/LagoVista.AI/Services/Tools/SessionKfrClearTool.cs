using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Clear all KFR entries for the current session branch.
    /// </summary>
    public sealed class SessionKfrClearTool : SessionKfrToolBase
    {
        public const string ToolName = "session_kfr_clear";
        public const string ToolSummary = "KFR: clear all entries for current branch";

        public const string ToolUsageMetadata = @"Tool Usage Instructions — KFR (Clear)

Purpose:
Truncate all Known Facts Registry (KFR) entries for the current session branch.

When to Call:
- Only when the user explicitly asks to reset/clear working memory.
- When you are instructed to start fresh and discard all KFR state.

Warnings:
- This is destructive for the current branch.
- Prefer evict/upsert for normal maintenance.
";

        public override string Name => ToolName;

        public SessionKfrClearTool(IAdminLogger logger, IAgentSessionManager sessions)
            : base(logger, sessions)
        {
        }

        public override Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (context?.Session == null)
                return Fail($"{ToolName} requires a valid execution context.");

            try
            {
                var list = GetBranchList(context);
                list.Clear();
                return Task.FromResult(Ok("clear", context, new List<AgentSessionKfrEntry>()));
            }
            catch (Exception ex)
            {
                Logger.AddException("[SessionKfrClearTool_ExecuteAsync__Exception]", ex);
                return Fail($"{ToolName} failed to process request.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Clear all KFR entries for the current session branch.",
                p => { });
        }
    }
}
