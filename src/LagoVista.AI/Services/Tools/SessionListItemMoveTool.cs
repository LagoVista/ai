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
    public sealed class SessionListItemMoveTool : SessionListToolBase
    {
        public const string ToolName = "session_list_item_move";
        public const string ToolSummary = "Move/reorder an item within a session list.";
        public const string ToolUsageMetadata = "Reorders items in a session-scoped list. Use when the user wants to move an item above/below another item or to a position.";

        public SessionListItemMoveTool(IAdminLogger logger, IAgentSessionManager sessions) : base(logger, sessions)
        {
        }

        public override string Name => ToolName;

        private sealed class Args
        {
            public string ListSlug { get; set; }
            public string ItemSlug { get; set; }

            // Move semantics
            public string AboveItemSlug { get; set; }
            public string BelowItemSlug { get; set; }
            public int? Position { get; set; }
        }

        public override Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
                return Fail("session_list_item_move requires a non-empty arguments object.");

            Args args;
            try { args = JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args(); }
            catch (Exception ex)
            {
                Logger.AddException("[SessionListItemMoveTool_Deserialize_Exception]", ex);
                return Fail("session_list_item_move failed to parse arguments.");
            }

            if (string.IsNullOrWhiteSpace(args.ListSlug))
                return Fail("session_list_item_move requires 'listSlug'.");

            if (string.IsNullOrWhiteSpace(args.ItemSlug))
                return Fail("session_list_item_move requires 'itemSlug'.");

            var list = FindList(context, args.ListSlug);
            if (list == null)
                return Fail($"List '{args.ListSlug}' not found.");

            var item = FindItem(list, args.ItemSlug);
            if (item == null)
                return Fail($"Item '{args.ItemSlug}' not found in list '{args.ListSlug}'.");

            var hasAnchor = !string.IsNullOrWhiteSpace(args.AboveItemSlug) || !string.IsNullOrWhiteSpace(args.BelowItemSlug);
            if (hasAnchor && args.Position.HasValue)
                return Fail("Provide either above/below OR position, not both.");

            if (!hasAnchor && !args.Position.HasValue)
                return Fail("Provide one of: aboveItemSlug, belowItemSlug, or position.");

            // Remove item from list, then insert
            list.Items.Remove(item);

            var ordered = list.Items.OrderBy(i => i.Order).ToList();

            if (args.Position.HasValue)
            {
                var pos = args.Position.Value;
                if (pos < 1) pos = 1;
                if (pos > ordered.Count + 1) pos = ordered.Count + 1;

                ordered.Insert(pos - 1, item);
            }
            else if (!string.IsNullOrWhiteSpace(args.AboveItemSlug))
            {
                var idx = ordered.FindIndex(i => string.Equals(i.Slug, args.AboveItemSlug, StringComparison.OrdinalIgnoreCase));
                if (idx < 0)
                    return Fail($"aboveItemSlug '{args.AboveItemSlug}' not found.");

                ordered.Insert(idx, item);
            }
            else
            {
                var idx = ordered.FindIndex(i => string.Equals(i.Slug, args.BelowItemSlug, StringComparison.OrdinalIgnoreCase));
                if (idx < 0)
                    return Fail($"belowItemSlug '{args.BelowItemSlug}' not found.");

                ordered.Insert(idx + 1, item);
            }

            list.Items = ordered;
            RenumberOrders(list);
            list.LastUpdatedDate = UtcStamp();

            return Task.FromResult(OkMove("move_item", list));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Move/reorder an item within a session list.", p =>
            {
                p.String("listSlug", "List slug.", required: true);
                p.String("itemSlug", "Item slug to move.", required: true);

                p.String("aboveItemSlug", "Move item above this item slug.");
                p.String("belowItemSlug", "Move item below this item slug.");
                p.Integer("position", "Move item to 1-based position.");
            });
        }
    }
}
