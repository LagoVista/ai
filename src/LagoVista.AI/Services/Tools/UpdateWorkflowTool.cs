using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Update an existing WorkflowDefinition used by the agent workflow registry (TUL-006).
    ///
    /// This is an authoring tool: it validates the workflow payload, calls the
    /// manager, and returns a WorkflowAuthoringResponse envelope.
    /// </summary>
    public sealed class UpdateWorkflowTool : IAgentTool
    {
        private readonly IWorkflowDefinitionManager _workflowManager;
        private readonly IAdminLogger _logger;

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = "Use this tool to update an existing workflow definition. Supply a 'workflow' object including its Id. The tool validates and persists the change, returning ok/messages/errors.";

        public const string ToolName = "agent_workflow_update";

        private sealed class UpdateWorkflowArgs
        {
            public WorkflowDefinition Workflow { get; set; }
        }

        public UpdateWorkflowTool(IWorkflowDefinitionManager workflowManager, IAdminLogger logger)
        {
            _workflowManager = workflowManager ?? throw new ArgumentNullException(nameof(workflowManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return InvokeResult<string>.FromError(
                    "UpdateWorkflowTool requires a non-empty arguments object with 'workflow'.");
            }

            UpdateWorkflowArgs args;
            try
            {
                args = JsonConvert.DeserializeObject<UpdateWorkflowArgs>(argumentsJson) ?? new UpdateWorkflowArgs();
            }
            catch (Exception ex)
            {
                _logger.AddException("[UpdateWorkflowTool_ExecuteAsync__DeserializeException]", ex);
                return InvokeResult<string>.FromError("UpdateWorkflowTool could not deserialize argumentsJson.");
            }

            var errors = new List<WorkflowAuthoringError>();

            if (args.Workflow == null)
            {
                errors.Add(new WorkflowAuthoringError
                {
                    Field = "workflow",
                    Message = "workflow is required."
                });

                var errorResponse = new WorkflowAuthoringResponse
                {
                    Ok = false,
                    Workflow = null,
                    Errors = errors
                };

                var errorJson = JsonConvert.SerializeObject(errorResponse);
                return InvokeResult<string>.Create(errorJson);
            }

            var wf = args.Workflow;

            if (string.IsNullOrWhiteSpace(wf.Id))
            {
                errors.Add(new WorkflowAuthoringError
                {
                    Field = "id",
                    Message = "Workflow Id is required for update."
                });
            }

            if (string.IsNullOrWhiteSpace(wf.Name))
            {
                errors.Add(new WorkflowAuthoringError
                {
                    Field = "name",
                    Message = "Workflow Name is required."
                });
            }

            if (string.IsNullOrWhiteSpace(wf.Description))
            {
                errors.Add(new WorkflowAuthoringError
                {
                    Field = "description",
                    Message = "Workflow Description is required."
                });
            }

            if (errors.Count > 0)
            {
                var validationResponse = new WorkflowAuthoringResponse
                {
                    Ok = false,
                    Workflow = wf,
                    Errors = errors
                };

                var validationJson = JsonConvert.SerializeObject(validationResponse);
                return InvokeResult<string>.Create(validationJson);
            }

            try
            {
                var result = await _workflowManager.UpdateWorkflowDefinitionAsync(wf, context?.Org, context?.User);

                var response = new WorkflowAuthoringResponse
                {
                    Ok = result.Successful,
                    Workflow = wf
                };

                if (!result.Successful)
                {
                    foreach (var err in result.Errors)
                    {
                        response.Errors.Add(new WorkflowAuthoringError
                        {
                            Field = err.ErrorCode,
                            Message = err.Message
                        });
                    }
                }
                else
                {
                    response.Messages.Add("Workflow updated successfully.");
                }

                var json = JsonConvert.SerializeObject(response);
                return InvokeResult<string>.Create(json);
            }
            catch (Exception ex)
            {
                _logger.AddException("[UpdateWorkflowTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("UpdateWorkflowTool failed to process the request.");
            }
        }

        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Update an existing workflow definition. The input workflow object must include its Id; the response contains ok/messages/errors and the updated workflow payload.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        workflow = new
                        {
                            type = "object",
                            description = "Workflow definition to update. Must include id, and should include name/description and other fields to persist."
                        }
                    },
                    required = new[] { "workflow" }
                }
            };

            return schema;
        }
    }
}
