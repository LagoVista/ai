using System;
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
    public sealed class SessionListSummaryItemListTool : SessionListToolBase
    {
        public const string ToolName = "session_list_summary_item_list";
        public const string ToolSummary = "List items in a session list (summary form).";
        public const string ToolUsageMetadata = "Lists items in a session list with minimal fields (slug, name, order). Use for quick display without full item payloads.";

        public SessionListSummaryItemListTool(IAdminLogger logger, IAgentSessionManager sessions) : base(logger, sessions)
        {
        }

        public override string Name => ToolName;

        private sealed class Args
        {
            public string ListSlug { get; set; }
        }

        public override Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
                return Fail("session_list_summary_item_list requires a non-empty arguments object.");

            Args args;
            try { args = JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args(); }
            catch (Exception ex)
            {
                Logger.AddException("[SessionListSummaryItemListTool_Deserialize_Exception]", ex);
                return Fail("session_list_summary_item_list failed to parse arguments.");
            }

            if (string.IsNullOrWhiteSpace(args.ListSlug))
                return Fail("session_list_summary_item_list requires 'listSlug'.");

            var list = FindList(context, args.ListSlug);
            if (list == null)
                return Fail($"List '{args.ListSlug}' not found.");

            var items = (list.Items ?? Enumerable.Empty<AgentSessionListItem>())
                .OrderBy(i => i.Order)
                .Select(i => new { i.Slug, i.Name, i.Order, i.Description })
                .ToList();

            return Task.FromResult(Ok("summary_item_list", new { listSlug = list.Slug, items }));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "List items in a session list (summary form).", p =>
            {
                p.String("listSlug", "List slug.", required: true);
            });
        }
    }
}
