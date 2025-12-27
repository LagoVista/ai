using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Tool that returns the full manifest (definition) for a single workflow.
    ///
    /// The LLM uses this tool when it has selected a specific workflow and needs
    /// the detailed instruction text, required inputs, permitted tools, and other
    /// metadata to execute the workflow.
    /// </summary>
    public sealed class GetWorkflowManifestTool : IAgentTool
    {
        private readonly IWorkflowDefinitionManager _workflowManager;
        private readonly IAdminLogger _logger;

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = "Use this tool after you have selected a specific workflow and need its full manifest. Supply the workflow id and this tool returns the detailed WorkflowDefinition used to drive execution.";

        public const string ToolName = "agent_workflow_manifest_get";


        public const string ToolSummary = "read a full workflow manifest";

        public GetWorkflowManifestTool(IWorkflowDefinitionManager workflowManager, IAdminLogger logger)
        {
            _workflowManager = workflowManager ?? throw new ArgumentNullException(nameof(workflowManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private sealed class GetWorkflowManifestArgs
        {
            public string WorkflowId { get; set; }
        }
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);

        public async Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return InvokeResult<string>.FromError(
                    "GetWorkflowManifestTool requires a non-empty arguments object with 'workflowId'.");
            }

            GetWorkflowManifestArgs args;
            try
            {
                args = JsonConvert.DeserializeObject<GetWorkflowManifestArgs>(argumentsJson) ?? new GetWorkflowManifestArgs();
            }
            catch (Exception ex)
            {
                _logger.AddException("[GetWorkflowManifestTool_ExecuteAsync__DeserializeException]", ex);
                return InvokeResult<string>.FromError("GetWorkflowManifestTool could not deserialize argumentsJson.");
            }

            if (string.IsNullOrWhiteSpace(args.WorkflowId))
            {
                return InvokeResult<string>.FromError(
                    "GetWorkflowManifestTool requires 'workflowId'.");
            }

            try
            {
                var definition = await _workflowManager.GetWorkflowDefinitionAsync(args.WorkflowId, context.Org, context.User);
                if (definition == null)
                {
                    return InvokeResult<string>.FromError($"Workflow '{args.WorkflowId}' was not found.");
                }

                if (definition.Status == WorkflowStatus.Disabled)
                {
                    return InvokeResult<string>.FromError($"Workflow '{args.WorkflowId}' is disabled and cannot be used.");
                }

                var response = new WorkflowRegistryManifestResponse
                {
                    Workflow = definition
                };

                var json = JsonConvert.SerializeObject(response);
                return InvokeResult<string>.Create(json);
            }
            catch (Exception ex)
            {
                _logger.AddException("[GetWorkflowManifestTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("GetWorkflowManifestTool failed to process the request.");
            }
        }

        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Get the full manifest for a single workflow by id, including instruction text, required inputs, permitted tools, and completion criteria.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        workflowId = new
                        {
                            type = "string",
                            description = "Identifier of the workflow to retrieve. This is the Id of the WorkflowDefinition."
                        }
                    },
                    required = new[] { "workflowId" }
                }
            };

            return schema;
        }
    }
}
