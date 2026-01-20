using System;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    public sealed class SessionListUpdateTool : SessionListToolBase
    {
        public const string ToolName = "session_list_update";
        public const string ToolSummary = "Update a session list's name/description.";
        public const string ToolUsageMetadata = "Updates list metadata (not schema). Use when the user wants to rename a list or change its description.";

        public SessionListUpdateTool(IAdminLogger logger, IAgentSessionManager sessions) : base(logger, sessions)
        {
        }

        public override string Name => ToolName;

        private sealed class Args
        {
            public string ListSlug { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
        }

        public override Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
                return Fail("session_list_update requires a non-empty arguments object.");

            Args args;
            try { args = JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args(); }
            catch (Exception ex)
            {
                Logger.AddException("[SessionListUpdateTool_Deserialize_Exception]", ex);
                return Fail("session_list_update failed to parse arguments.");
            }

            if (string.IsNullOrWhiteSpace(args.ListSlug))
                return Fail("session_list_update requires 'listSlug'.");

            var list = FindList(context, args.ListSlug);
            if (list == null)
                return Fail($"List '{args.ListSlug}' not found.");

            if (!string.IsNullOrWhiteSpace(args.Name))
                list.Name = args.Name.Trim();

            if (args.Description != null)
                list.Description = string.IsNullOrWhiteSpace(args.Description) ? null : args.Description.Trim();

            list.LastUpdatedDate = UtcStamp();

            return Task.FromResult(OkUpdated("update", list));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Update a session list's name/description.", p =>
            {
                p.String("listSlug", "List slug.", required: true);
                p.String("name", "New list name.");
                p.String("description", "New list description (set to empty string to clear).");
            });
        }
    }
}
