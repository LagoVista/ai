using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Services.Tools
{
    public class ActivateToolsTool : IAgentTool
    {
        private readonly IAdminLogger _adminLogger;
        private readonly IServerToolUsageMetadataProvider _metaDataProvider;
        private readonly IServerToolSchemaProvider _schemaProvider;
        private readonly IAgentStreamingContext _streamingContext;

        public ActivateToolsTool(IAdminLogger adminLogger, IAgentStreamingContext streamingContext, IServerToolUsageMetadataProvider metaDataProvider, IServerToolSchemaProvider schemaProvider)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _schemaProvider = schemaProvider ?? throw new ArgumentNullException(nameof(schemaProvider));
            _metaDataProvider = metaDataProvider ?? throw new ArgumentNullException(nameof(metaDataProvider));
            _streamingContext = streamingContext ?? throw new ArgumentNullException(nameof(streamingContext));
        }

        public bool IsToolFullyExecutedOnServer => true;


        public const string ToolUsageMetadata =
@"Use this tool to request tools be active in the next request so they can process your request.  
The prior request will be replayed.  
Requested tools become available starting the next turn.";

        public const string ToolSummary = "activate tools for next request (always available)";

        public const string ToolName = "activate_tools";

        public string Name => ToolName;

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            JObject payload;

            var tag = "[LoadToolsTool__ExecuteAsync]";

            try
            {
                if (string.IsNullOrWhiteSpace(argumentsJson))
                {
                    payload = new JObject();
                    _adminLogger.AddError(tag, "No arguments provided");
                    return Task.FromResult(InvokeResult<string>.FromError("argumentsJson was not valid JSON."));
                }
                else
                {
                    payload = JObject.Parse(argumentsJson);
                    var idsArray = payload["tool_ids"] as JArray;
                    var toolIds = idsArray.Values<string>().ToList();

                    foreach (var tool in toolIds)
                    {
                        var lane = context.PromptKnowledgeProvider.Registers.SingleOrDefault(r => r.Classification == Models.Context.ContextClassification.Session);
                        var usageInstructions = _metaDataProvider.GetToolUsageMetadata(tool);
                        lane.Items.Add($"### {tool}\r\n{usageInstructions}\r\n\r\n");

                        context.PromptKnowledgeProvider.ActiveTools.Add(tool);
                    }

                    _streamingContext.AddMilestoneAsync($"using tools {String.Join(',', toolIds)}");
                }
            }
            catch (JsonException jex)
            {
                _adminLogger.AddException(tag, jex);
                return Task.FromResult( InvokeResult<string>.FromError("argumentsJson was not valid JSON."));
            }

            return Task.FromResult(InvokeResult<string>.Create("{'result':'ok'}"));

        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Tool that can be used to activate tools in the next call.", p =>
            {
                p.StringArray("tool_ids", "tool ids from the available tool blocks that should be activated");
            });
        }
    }
}
