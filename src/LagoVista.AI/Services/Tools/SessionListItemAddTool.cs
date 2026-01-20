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
    public sealed class SessionListItemAddTool : SessionListToolBase
    {
        public const string ToolName = "session_list_item_add";
        public const string ToolSummary = "Add an item to a session list.";
        public const string ToolUsageMetadata = "Adds an item to a session-scoped list. Use when the user wants to add something to a list.";

        public SessionListItemAddTool(IAdminLogger logger, IAgentSessionManager sessions) : base(logger, sessions)
        {
        }

        public override string Name => ToolName;

        private sealed class Args
        {
            public string ListSlug { get; set; }
            public string Name { get; set; }
            public string Slug { get; set; }
            public string Description { get; set; }
            public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public override Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
                return Fail("session_list_item_add requires a non-empty arguments object.");

            Args args;
            try { args = JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args(); }
            catch (Exception ex)
            {
                Logger.AddException("[SessionListItemAddTool_Deserialize_Exception]", ex);
                return Fail("session_list_item_add failed to parse arguments.");
            }

            if (string.IsNullOrWhiteSpace(args.ListSlug))
                return Fail("session_list_item_add requires 'listSlug'.");

            if (string.IsNullOrWhiteSpace(args.Name))
                return Fail("session_list_item_add requires a non-empty 'name'.");

            var list = FindList(context, args.ListSlug);
            if (list == null)
                return Fail($"List '{args.ListSlug}' not found.");

            var desiredSlug = string.IsNullOrWhiteSpace(args.Slug) ? Slugify(args.Name) : Slugify(args.Slug);
            var itemSlug = EnsureUniqueItemSlug(list, desiredSlug);

            var validation = ValidateAgainstSchema(list, args.Data, out var error);
            if (validation != null)
                return Task.FromResult(validation);

            var now = UtcStamp();

            var item = new AgentSessionListItem
            {
                Id = Guid.NewGuid(),
                ListId = list.Id,
                Name = args.Name.Trim(),
                Slug = itemSlug,
                Description = string.IsNullOrWhiteSpace(args.Description) ? null : args.Description.Trim(),
                Data = args.Data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Order = NextOrder(list),
                CreationDate = now,
                LastUpdatedDate = now
            };

            list.Items.Add(item);
            list.LastUpdatedDate = now;

            return Task.FromResult(OkItemCreated("add_item", list, item));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Add an item to a session list.", p =>
            {
                p.String("listSlug", "List slug.", required: true);
                p.String("name", "Item name.", required: true);
                p.String("slug", "Optional item slug (unique within list). If omitted, generated from name.");
                p.String("description", "Optional item description.");
                p.Object("data", "Optional metadata values keyed by field key.", obj =>
                {
                    obj.AdditionalProperties = true;
                });
            });
        }
    }
}
