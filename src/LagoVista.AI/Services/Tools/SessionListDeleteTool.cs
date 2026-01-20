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
    public sealed class SessionListDeleteTool : SessionListToolBase
    {
        public const string ToolName = "session_list_delete";
        public const string ToolSummary = "Delete a session list by slug.";
        public const string ToolUsageMetadata = "Deletes a session-scoped list. Use when the user wants to remove a list entirely.";

        public SessionListDeleteTool(IAdminLogger logger, IAgentSessionManager sessions) : base(logger, sessions)
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
                return Fail("session_list_delete requires a non-empty arguments object.");

            Args args;
            try { args = JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args(); }
            catch (Exception ex)
            {
                Logger.AddException("[SessionListDeleteTool_Deserialize_Exception]", ex);
                return Fail("session_list_delete failed to parse arguments.");
            }

            if (string.IsNullOrWhiteSpace(args.ListSlug))
                return Fail("session_list_delete requires 'listSlug'.");

            EnsureLists(context);

            var list = FindList(context, args.ListSlug);
            if (list == null)
                return Fail($"List '{args.ListSlug}' not found.");

            context.Session.Lists.Remove(list);

            return Task.FromResult(OkDeleted("delete", list.Slug));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Delete a session list by slug.", p =>
            {
                p.String("listSlug", "List slug.", required: true);
            });
        }
    }
}
