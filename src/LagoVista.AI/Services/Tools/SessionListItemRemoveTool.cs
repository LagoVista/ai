using System;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    public sealed class SessionListItemRemoveTool : SessionListToolBase
    {
        public const string ToolName = "session_list_item_remove";
        public const string ToolSummary = "Remove an item from a session list.";
        public const string ToolUsageMetadata = "Removes an item from a session-scoped list.";

        public SessionListItemRemoveTool(IAdminLogger logger, IAgentSessionManager sessions) : base(logger, sessions)
        {
        }

        public override string Name => ToolName;

        private sealed class Args
        {
            public string ListSlug { get; set; }
            public string ItemSlug { get; set; }
        }

        public override Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
                return Fail("session_list_item_remove requires a non-empty arguments object.");

            Args args;
            try { args = JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args(); }
            catch (Exception ex)
            {
                Logger.AddException("[SessionListItemRemoveTool_Deserialize_Exception]", ex);
                return Fail("session_list_item_remove failed to parse arguments.");
            }

            if (string.IsNullOrWhiteSpace(args.ListSlug))
                return Fail("session_list_item_remove requires 'listSlug'.");

            if (string.IsNullOrWhiteSpace(args.ItemSlug))
                return Fail("session_list_item_remove requires 'itemSlug'.");

            var list = FindList(context, args.ListSlug);
            if (list == null)
                return Fail($"List '{args.ListSlug}' not found.");

            var item = FindItem(list, args.ItemSlug);
            if (item == null)
                return Fail($"Item '{args.ItemSlug}' not found in list '{args.ListSlug}'.");

            list.Items.Remove(item);
            list.LastUpdatedDate = UtcStamp();

            return Task.FromResult(OkDeleted("remove_item", list.Slug, args.ItemSlug));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Remove an item from a session list.", p =>
            {
                p.String("listSlug", "List slug.", required: true);
                p.String("itemSlug", "Item slug.", required: true);
            });
        }
    }
}
