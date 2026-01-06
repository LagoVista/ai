using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    public sealed class GetCurrentUser : IAgentTool
    {
        public const string ToolName = "get_current_user";
        public const string ToolUsageMetadata = @"Returns the currently authenticated user as an EntityHeader. Use for CreatedBy / LastUpdatedBy.
Example Response:
{
  ""currentUser"": {
    ""id"": ""usr_7f92a31d"",
    ""text"": ""Kevin Wolf""
  }
}
";
        public const string ToolSummary = "Get current authenticated user (for CreatedBy / LastUpdatedBy).";

        public GetCurrentUser()
        {
        }

        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        private sealed class CurrentUserResult
        {
            [JsonProperty("currentUser")]
            public EntityHeader CurrentUser { get; set; }
        }


        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            var reply = new CurrentUserResult
            {
                CurrentUser = context.User
            };
            var json = JsonConvert.SerializeObject(reply);
            return Task.FromResult(InvokeResult<string>.Create(json));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Get the currently authenticated user as an EntityHeader (id, text). No arguments",
                p =>
                {
                });
        }
    }
}