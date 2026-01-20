using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    public sealed class SessionListItemUpdateTool : SessionListToolBase
    {
        public const string ToolName = "session_list_item_update";
        public const string ToolSummary = "Update an item in a session list.";
        public const string ToolUsageMetadata = "Updates an item (name/description/slug/data) in a session-scoped list.";

        public SessionListItemUpdateTool(IAdminLogger logger, IAgentSessionManager sessions) : base(logger, sessions)
        {
        }

        public override string Name => ToolName;

        private sealed class Args
        {
            public string ListSlug { get; set; }
            public string ItemSlug { get; set; }
            public string Name { get; set; }
            public string NewSlug { get; set; }
            public string Description { get; set; }
            public Dictionary<string, string> Data { get; set; }
        }

        public override Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
                return Fail("session_list_item_update requires a non-empty arguments object.");

            Args args;
            try { args = JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args(); }
            catch (Exception ex)
            {
                Logger.AddException("[SessionListItemUpdateTool_Deserialize_Exception]", ex);
                return Fail("session_list_item_update failed to parse arguments.");
            }

            if (string.IsNullOrWhiteSpace(args.ListSlug))
                return Fail("session_list_item_update requires 'listSlug'.");

            if (string.IsNullOrWhiteSpace(args.ItemSlug))
                return Fail("session_list_item_update requires 'itemSlug'.");

            var list = FindList(context, args.ListSlug);
            if (list == null)
                return Fail($"List '{args.ListSlug}' not found.");

            var item = FindItem(list, args.ItemSlug);
            if (item == null)
                return Fail($"Item '{args.ItemSlug}' not found in list '{args.ListSlug}'.");

            if (!string.IsNullOrWhiteSpace(args.Name))
                item.Name = args.Name.Trim();

            if (args.Description != null)
                item.Description = string.IsNullOrWhiteSpace(args.Description) ? null : args.Description.Trim();

            if (args.Data != null)
            {
                var validation = ValidateAgainstSchema(list, args.Data, out var error);
                if (validation != null)
                    return Task.FromResult(validation);

                item.Data = args.Data;
            }

            if (!string.IsNullOrWhiteSpace(args.NewSlug))
            {
                var desired = Slugify(args.NewSlug);
                if (string.IsNullOrWhiteSpace(desired))
                    return Fail("newSlug produced an empty slug.");

                // remove current item temporarily to allow keeping same slug
                list.Items.Remove(item);
                var unique = EnsureUniqueItemSlug(list, desired);
                list.Items.Add(item);

                item.Slug = unique;
            }

            var now = UtcStamp();
            item.LastUpdatedDate = now;
            list.LastUpdatedDate = now;

            return Task.FromResult(OkItemUpdated("update_item", list, item));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Update an item in a session list.", p =>
            {
                p.String("listSlug", "List slug.", required: true);
                p.String("itemSlug", "Current item slug.", required: true);
                p.String("name", "New item name.");
                p.String("newSlug", "New item slug.");
                p.String("description", "New description (set to empty string to clear).");
                p.Object("data", "Optional metadata values keyed by field key.", obj =>
                {
                    obj.AdditionalProperties = true;
                });
            });
        }
    }
}
