using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    public sealed class GetCurrentTimeStamp  : IAgentTool
    {
        public const string ToolName = "get_current_timestamp";
        public const string ToolUsageMetadata =
  "Use this tool when you need the current time. Returns JSON { status: 'ok', timestamp: '<ISO 8601 UTC>' }. No arguments. Do not fabricate timestamps.";

        public const string ToolSummary = "Get the current UTC timestamp (ISO 8601)";

        public GetCurrentTimeStamp()
        {
        }

        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        private sealed class TimeStampResult
        {
            [JsonProperty("status")]
            public string Status { get; set; }

            [JsonProperty("timestamp")]
            public string Timestamp { get; set; }
        }


        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {  
            var reply = new TimeStampResult
            {
                Status = "ok",
                Timestamp = DateTime.UtcNow.ToJSONString()
            };
            var json = JsonConvert.SerializeObject(reply);
            return Task.FromResult(InvokeResult<string>.Create(json));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Returns the current UTC date/time as an ISO 8601 timestamp. The response is JSON with fields: status and timestamp. Call with no arguments.", 
                p =>
            {
            });
        }
    }
}