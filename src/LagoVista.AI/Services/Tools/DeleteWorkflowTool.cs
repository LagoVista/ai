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
    /// Delete an existing WorkflowDefinition used by the agent workflow registry (TUL-006).
    ///
    /// This is an authoring tool: it calls the manager delete operation and returns a
    /// WorkflowAuthoringResponse envelope with ok/messages/errors.
    /// </summary>
    public sealed class DeleteWorkflowTool : IAgentTool
    {
        private readonly IWorkflowDefinitionManager _workflowManager;
        private readonly IAdminLogger _logger;

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = "Use this tool to delete an existing workflow definition by id. The response indicates whether the delete succeeded and returns messages/errors.";

        public const string ToolName = "agent_workflow_delete";

        private sealed class DeleteWorkflowArgs
        {
            public string WorkflowId { get; set; }
        }

        public DeleteWorkflowTool(IWorkflowDefinitionManager workflowManager, IAdminLogger logger)
        {
            _workflowManager = workflowManager ?? throw new ArgumentNullException(nameof(workflowManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);

        public async Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return InvokeResult<string>.FromError(
                    "DeleteWorkflowTool requires a non-empty arguments object with 'workflowId'.");
            }

            DeleteWorkflowArgs args;
            try
            {
                args = JsonConvert.DeserializeObject<DeleteWorkflowArgs>(argumentsJson) ?? new DeleteWorkflowArgs();
            }
            catch (Exception ex)
            {
                _logger.AddException("[DeleteWorkflowTool_ExecuteAsync__DeserializeException]", ex);
                return InvokeResult<string>.FromError("DeleteWorkflowTool could not deserialize argumentsJson.");
            }

            var errors = new List<WorkflowAuthoringError>();

            if (string.IsNullOrWhiteSpace(args.WorkflowId))
            {
                errors.Add(new WorkflowAuthoringError
                {
                    Field = "workflowId",
                    Message = "workflowId is required."
                });

                var errorResponse = new WorkflowAuthoringResponse
                {
                    Ok = false,
                    Errors = errors
                };

                var errorJson = JsonConvert.SerializeObject(errorResponse);
                return InvokeResult<string>.Create(errorJson);
            }

            try
            {
                var result = await _workflowManager.DeleteWorkflowDefinitionAsync(args.WorkflowId, context?.Org, context?.User);

                var response = new WorkflowAuthoringResponse
                {
                    Ok = result.Successful
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
                    response.Messages.Add($"Workflow '{args.WorkflowId}' deleted successfully.");
                }

                var json = JsonConvert.SerializeObject(response);
                return InvokeResult<string>.Create(json);
            }
            catch (Exception ex)
            {
                _logger.AddException("[DeleteWorkflowTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("DeleteWorkflowTool failed to process the request.");
            }
        }

        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Delete an existing workflow definition by id. The response indicates success and includes messages/errors.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        workflowId = new
                        {
                            type = "string",
                            description = "Identifier (Id) of the workflow to delete."
                        }
                    },
                    required = new[] { "workflowId" }
                }
            };

            return schema;
        }
    }
}
