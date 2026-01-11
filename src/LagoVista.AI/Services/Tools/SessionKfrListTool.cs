using System;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// List active KFR entries for the current session branch.
    /// </summary>
    public sealed class SessionKfrListTool : SessionKfrToolBase
    {
        public const string ToolName = "session_kfr_list";
        public const string ToolSummary = "KFR: list active working-memory entries";

        public const string ToolUsageMetadata = @"Tool Usage Instructions — KFR (List)

Purpose:
Return all active Known Facts Registry (KFR) entries for the current session branch so the user (and assistant) can see current working memory.

When to Call:
- When you need to display the current KFR contents.
- When the user asks to see, review, or confirm the active KFR.

Do Not Call:
- As part of normal KFR maintenance; use upsert/evict instead.
";

        public override string Name => ToolName;

        public SessionKfrListTool(IAdminLogger logger, IAgentSessionManager sessions)
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
                var items = list.Where(k => k.IsActive).ToList();
                return Task.FromResult(Ok("list", context, items));
            }
            catch (Exception ex)
            {
                Logger.AddException("[SessionKfrListTool_ExecuteAsync__Exception]", ex);
                return Fail($"{ToolName} failed to process request.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "List active KFR entries for the current session branch.",
                p => { });
        }
    }
}
