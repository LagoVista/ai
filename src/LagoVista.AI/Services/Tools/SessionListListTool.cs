using System;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    public sealed class SessionListListTool : SessionListToolBase
    {
        public const string ToolName = "session_list_list";
        public const string ToolSummary = "List all lists on the current session.";
        public const string ToolUsageMetadata = "Lists session-scoped lists. Use when the user asks what lists exist.";

        public SessionListListTool(IAdminLogger logger, IAgentSessionManager sessions) : base(logger, sessions)
        {
        }

        public override string Name => ToolName;

        private sealed class Args
        {
            public bool IncludeItems { get; set; }
        }

        public override Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            Args args = new Args();
            if (!string.IsNullOrWhiteSpace(argumentsJson))
            {
                try { args = JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args(); }
                catch { /* ignore, default */ }
            }

            EnsureLists(context);

            if (args.IncludeItems)
                return Task.FromResult(OkLists("list", context.Session.Lists));

            var summaries = context.Session.Lists
                .OrderBy(l => l.Slug)
                .Select(l => new { l.Slug, l.Name, l.Description, itemCount = l.Items?.Count ?? 0, fieldCount = l.Fields?.Count ?? 0, l.LastUpdatedDate })
                .ToList();

            return Task.FromResult(Ok("list", new { lists = summaries }));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "List all lists on the current session.", p =>
            {
                p.Boolean("includeItems", "If true, returns full list documents including items.");
            });
        }
    }
}
