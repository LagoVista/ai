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
    public sealed class GetAuditFieldsForUpdate : IAgentTool
    {
        public const string ToolName = "get_audit_stamp_for_update";

        public const string ToolUsageMetadata =
        @"Use this tool whenever updating an existing record that implements IAuditableEntitySimple.
Returns authoritative LastUpdatedDate (ISO 8601 UTC) and LastUpdatedBy (EntityHeader). No arguments. Do not fabricate these values.
Example:
{ ""lastUpdatedDate"": ""2026-01-05T16:18:09.110Z"", ""lastUpdatedBy"": { ""id"": ""usr_123"", ""text"": ""Kevin Wolf"" } }";

        public const string ToolSummary = "Get audit fields for update (LastUpdatedBy, LastUpdatedDate).";

        public GetAuditFieldsForUpdate()
        {
        }

        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;


        private sealed class AuditStampForUpdate
        {
            [JsonProperty("lastUpdatedDate")]
            public string LastUpdatedDate { get; set; }

            [JsonProperty("lastUpdatedBy")]
            public EntityHeader LastUpdatedBy { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            throw new ArgumentNullException(); 
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Get audit fields for updating an existing record. Returns: " +
                "{ lastUpdatedDate: string(ISO 8601 UTC), lastUpdatedBy: { id: string, text: string } }. No arguments.",
                p => { }
            );
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            var reply = new AuditStampForUpdate
            {
                LastUpdatedDate = DateTime.UtcNow.ToJSONString(),
                LastUpdatedBy = context.Envelope.User, // ensure this is EntityHeader
            };

            return Task.FromResult(InvokeResult<string>.Create(JsonConvert.SerializeObject(reply)));
        }
    }
}