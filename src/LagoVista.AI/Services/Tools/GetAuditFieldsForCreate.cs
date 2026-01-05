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
    public sealed class GetAuditFieldsForCreate : IAgentTool
    {
        public const string ToolName = "get_audit_stamp_for_create";

        public const string ToolUsageMetadata =
          "Use this tool when creating any new record that implements EntityBase. " +
          "Returns authoritative audit fields and OwnerOrganization. No arguments. " +
          "Do not fabricate CreatedBy, LastUpdatedBy, OwnerOrganization, or timestamps. " +
          "If audit fields are required and not already present, the model must call this tool.";

        public const string ToolSummary = "Get audit fields for create (CreatedBy/LastUpdatedBy, dates, OwnerOrganization).";

        public GetAuditFieldsForCreate()
        {
        }

        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        private sealed class AuditStampForCreate
        {
            [JsonProperty("creationDate")]
            public String CreationDate { get; set; }
            [JsonProperty("lastUpdatedDate")]
            public String LastUpdatedDate { get; set; }
            [JsonProperty("createdBy")]
            public EntityHeader CreatedBy { get; set; }
            [JsonProperty("lastUpdatedBy")]
            public EntityHeader LastUpdatedBy { get; set; }
            [JsonProperty("ownerOrganization")]
            public EntityHeader OwnerOrganization { get; set; }
        }


        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            var timeStamp = DateTime.UtcNow.ToJSONString();

            var reply = new AuditStampForCreate
            {
                CreatedBy = context.User,
                LastUpdatedBy = context.User,
                CreationDate = timeStamp,
                LastUpdatedDate = timeStamp,
                OwnerOrganization = context.Org

            };
            var json = JsonConvert.SerializeObject(reply);
            return Task.FromResult(InvokeResult<string>.Create(json));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, 
                     "Get audit fields for creating a new record. Returns: " +
                    "{ creationDate: string(ISO 8601 UTC), lastUpdatedDate: string(ISO 8601 UTC), " +
                    "createdBy: { id: string, text: string }, lastUpdatedBy: { id: string, text: string }, " +
                    "ownerOrganization: { id: string, text: string } }. No arguments.", p =>
                {
                });
        }
    }
}