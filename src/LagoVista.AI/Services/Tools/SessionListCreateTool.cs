using System;
using System.Collections.Generic;
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
    public sealed class SessionListCreateTool : SessionListToolBase
    {
        public const string ToolName = "session_list_create";
        public const string ToolSummary = "Create a session-scoped list with optional schema fields.";
        public const string ToolUsageMetadata = "Creates a named list stored on the current session. Use when the user wants to create a list to track items.";

        public SessionListCreateTool(IAdminLogger logger, IAgentSessionManager sessions) : base(logger, sessions)
        {
        }

        public override string Name => ToolName;

        private sealed class Args
        {
            public string Name { get; set; }
            public string Slug { get; set; }
            public string Description { get; set; }
            public List<FieldArgs> Fields { get; set; } = new List<FieldArgs>();
        }

        private sealed class FieldArgs
        {
            public string Key { get; set; }
            public string Label { get; set; }
            public string Type { get; set; }
            public bool Required { get; set; }
            public List<string> EnumValues { get; set; } = new List<string>();
            public int SortOrder { get; set; }
        }

        public override Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
                return Fail("session_list_create requires a non-empty arguments object.");

            Args args;
            try
            {
                args = JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args();
            }
            catch (Exception ex)
            {
                Logger.AddException("[SessionListCreateTool_Deserialize_Exception]", ex);
                return Fail("session_list_create failed to parse arguments.");
            }

            if (string.IsNullOrWhiteSpace(args.Name))
                return Fail("session_list_create requires a non-empty 'name'.");

            EnsureLists(context);

            var desiredSlug = string.IsNullOrWhiteSpace(args.Slug) ? Slugify(args.Name) : Slugify(args.Slug);
            var slug = EnsureUniqueListSlug(context, desiredSlug);

            var list = new AgentSessionListDefinition
            {
                Id = Guid.NewGuid(),
                Name = args.Name?.Trim(),
                Slug = slug,
                Description = string.IsNullOrWhiteSpace(args.Description) ? null : args.Description.Trim(),
                SchemaVersion = 1,
                CreationDate = UtcStamp(),
                LastUpdatedDate = UtcStamp(),
                Fields = new List<AgentSessionFieldDefinition>(),
                Items = new List<AgentSessionListItem>()
            };

            if (args.Fields != null && args.Fields.Count > 0)
            {
                foreach (var f in args.Fields.OrderBy(f => f.SortOrder))
                {
                    if (string.IsNullOrWhiteSpace(f.Label))
                        return Fail("session_list_create fields require a non-empty 'label'.");

                    var key = string.IsNullOrWhiteSpace(f.Key) ? Slugify(f.Label).Replace("-", "_") : f.Key.Trim();
                    if (string.IsNullOrWhiteSpace(key))
                        return Fail("session_list_create fields require a non-empty 'key'.");

                    if (list.Fields.Any(existing => string.Equals(existing.Key, key, StringComparison.OrdinalIgnoreCase)))
                        return Fail($"Duplicate field key '{key}'.");

                    if (!Enum.TryParse<AgentSessionListFieldDataType>(f.Type, ignoreCase: true, out var fieldType))
                        return Fail($"Invalid field type '{f.Type}'.");

                    if (fieldType == AgentSessionListFieldDataType.Enum && (f.EnumValues == null || f.EnumValues.Count == 0))
                        return Fail($"Enum field '{key}' requires enumValues.");

                    list.Fields.Add(new AgentSessionFieldDefinition
                    {
                        Key = key,
                        Label = f.Label.Trim(),
                        Type = fieldType,
                        Required = f.Required,
                        EnumValues = f.EnumValues ?? new List<string>(),
                        SortOrder = f.SortOrder
                    });
                }
            }

            context.Session.Lists.Add(list);

            // Session persistence handled elsewhere.
            return Task.FromResult(OkListCreated("create", list));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Create a session-scoped list with optional schema fields.", p =>
            {
                p.String("name", "List name.", required: true);
                p.String("slug", "Optional list slug (unique within session). If omitted, generated from name.");
                p.String("description", "Optional list description.");

                p.ObjectArray("fields", "Optional schema fields for items.", item =>
                {
                    item.String("label", "Display label for the field.", required: true);
                    item.String("key", "Optional key used in item.data. If omitted, generated from label.");
                    item.String("type", "Field type: Text, Number, Bool, Date, DateTime, Enum.", required: true);
                    item.Boolean("required", "Whether this field is required.");
                    item.StringArray("enumValues", "Allowed values when type == Enum.");
                    item.Integer("sortOrder", "Sort order for prompting/display.");
                });
            });
        }
    }
}
