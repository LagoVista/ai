using System;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    public sealed class SessionListGetTool : SessionListToolBase
    {
        public const string ToolName = "session_list_get";
        public const string ToolSummary = "Get a session list by slug.";
        public const string ToolUsageMetadata = "Retrieves a session-scoped list. Use when the user asks to view a list and its items.";

        public SessionListGetTool(IAdminLogger logger, IAgentSessionManager sessions) : base(logger, sessions)
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
                return Fail("session_list_get requires a non-empty arguments object.");

            Args args;
            try { args = JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args(); }
            catch (Exception ex)
            {
                Logger.AddException("[SessionListGetTool_Deserialize_Exception]", ex);
                return Fail("session_list_get failed to parse arguments.");
            }

            if (string.IsNullOrWhiteSpace(args.ListSlug))
                return Fail("session_list_get requires 'listSlug'.");

            var list = FindList(context, args.ListSlug);
            if (list == null)
                return Fail($"List '{args.ListSlug}' not found.");

            return Task.FromResult(OkList("get", list));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Get a session list by slug.", p =>
            {
                p.String("listSlug", "List slug.", required: true);
            });
        }
    }
}
