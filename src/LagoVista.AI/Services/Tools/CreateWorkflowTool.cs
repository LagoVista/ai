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
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Create a new WorkflowDefinition used by the agent workflow registry (TUL-006).
    ///
    /// This is an authoring tool: it validates the workflow payload, calls the
    /// manager, and returns a WorkflowAuthoringResponse envelope with ok/messages/errors.
    /// </summary>
    public sealed class CreateWorkflowTool : IAgentTool
    {
        private readonly IWorkflowDefinitionManager _workflowManager;
        private readonly IAdminLogger _logger;
        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = @"
### Workflow Proposal Binding (Critical Rule)

When you output a workflow proposal in JSON form, you MUST treat that JSON
as the **active workflow proposal** for the rest of the conversation until:

- The user approves it (�Yes�, �Create it�, �Do it�, �That�s good�, etc.)
- The user rejects it
- You replace it with a new proposal

You MUST remember the most recent JSON workflow proposal you generated.
This is NOT optional. You must carry it forward across turns.

When the user confirms a proposal:
- You MUST use the exact JSON object you previously generated.
- You must NOT ask the user to repeat details.
- You must NOT claim that no proposal exists.
- You must NOT regenerate a new proposal.
- You must immediately call the appropriate workflow tool with:
  {
    ""workflow"": { ...all fields exactly as you proposed... }
  }

To be explicit:
If you previously printed:

{
  ""workflow"": { ...some object... }
}

then that **is** the pending workflow proposal. On user confirmation, ALWAYS
use that object to call the workflow tool.

If you ever say �I don�t have a pending workflow proposal� you have violated
this rule.

";
        public const string ToolSummary = "create a workflow to be used as an instruction";
        public const string ToolName = "agent_workflow_create";
        public CreateWorkflowTool(IWorkflowDefinitionManager workflowManager, IAdminLogger logger)
        {
            _workflowManager = workflowManager ?? throw new ArgumentNullException(nameof(workflowManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            Console.WriteLine("=====\r\nEXECUTE CREATE WORKFLOW TOOL WITH ARGUMENTS ====>>>\r\n" + argumentsJson + "====\r\n");
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return InvokeResult<string>.FromError("CreateWorkflowTool requires a non-empty arguments object with 'workflow'.");
            }

            JObject root;
            try
            {
                root = JObject.Parse(argumentsJson);
            }
            catch (Exception ex)
            {
                _logger.AddException("[CreateWorkflowTool_ExecuteAsync__ParseException]", ex);
                return InvokeResult<string>.FromError("CreateWorkflowTool could not parse argumentsJson as JSON.");
            }

            var errors = new List<WorkflowAuthoringError>();
            var workflowToken = root["workflow"] ?? root["Workflow"];
            if (workflowToken == null || workflowToken.Type != JTokenType.Object)
            {
                errors.Add(new WorkflowAuthoringError { Field = "workflow", Message = "workflow object is required." });
                var errorResponse = new WorkflowAuthoringResponse
                {
                    Ok = false,
                    Workflow = null,
                    Errors = errors
                };
                var errorJson = JsonConvert.SerializeObject(errorResponse);
                return InvokeResult<string>.Create(errorJson);
            }

            var wfObj = (JObject)workflowToken;
            // Manually map into WorkflowDefinition to be resilient to casing and extra fields.
            var wf = new WorkflowDefinition
            {
                Id = (string)wfObj["id"] ?? (string)wfObj["Id"],
                Name = (string)wfObj["name"] ?? (string)wfObj["Name"],
                Description = (string)wfObj["description"] ?? (string)wfObj["Description"],
                Version = (string)wfObj["version"] ?? (string)wfObj["Version"],
                InstructionText = (string)wfObj["instructionText"] ?? (string)wfObj["InstructionText"],
                Notes = (string)wfObj["notes"] ?? (string)wfObj["Notes"]
            };
            // Optional: status (string or int)
            var statusToken = wfObj["status"] ?? wfObj["Status"];
            if (statusToken != null && statusToken.Type != JTokenType.Null)
            {
                try
                {
                    if (statusToken.Type == JTokenType.Integer)
                    {
                        wf.Status = (WorkflowStatus)statusToken.Value<int>();
                    }
                    else if (statusToken.Type == JTokenType.String)
                    {
                        if (Enum.TryParse<WorkflowStatus>(statusToken.Value<string>(), true, out var status))
                        {
                            wf.Status = status;
                        }
                    }
                }
                catch
                {
                // Ignore and leave default
                }
            }

            // Optional: visibility (string or int)
            var visibilityToken = wfObj["visibility"] ?? wfObj["Visibility"];
            if (visibilityToken != null && visibilityToken.Type != JTokenType.Null)
            {
                try
                {
                    if (visibilityToken.Type == JTokenType.Integer)
                    {
                        wf.Visibility = (WorkflowVisibility)visibilityToken.Value<int>();
                    }
                    else if (visibilityToken.Type == JTokenType.String)
                    {
                        if (Enum.TryParse<WorkflowVisibility>(visibilityToken.Value<string>(), true, out var visibility))
                        {
                            wf.Visibility = visibility;
                        }
                    }
                }
                catch
                {
                // Ignore and leave default
                }
            }

            // Optional arrays � for now we treat them as best-effort; if the LLM sends
            // weird shapes here we just skip rather than throw.
            try
            {
                var intentsToken = wfObj["userIntentPatterns"] ?? wfObj["UserIntentPatterns"];
                if (intentsToken is JArray intentsArray)
                {
                    foreach (var t in intentsArray)
                    {
                        var val = t.Value<string>();
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            wf.UserIntentPatterns.Add(val);
                        }
                    }
                }

                var toolsToken = wfObj["permittedTools"] ?? wfObj["PermittedTools"];
                if (toolsToken is JArray toolsArray)
                {
                    foreach (var t in toolsArray)
                    {
                        var val = t.Value<string>();
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            wf.PermittedTools.Add(val);
                        }
                    }
                }

                var preconditionsToken = wfObj["preconditions"] ?? wfObj["Preconditions"];
                if (preconditionsToken is JArray preArray)
                {
                    foreach (var t in preArray)
                    {
                        var val = t.Value<string>();
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            wf.Preconditions.Add(val);
                        }
                    }
                }

                var completionToken = wfObj["completionCriteria"] ?? wfObj["CompletionCriteria"];
                if (completionToken != null && completionToken.Type != JTokenType.Null)
                {
                    wf.CompletionCriteria = completionToken.Value<string>();
                }
            }
            catch (Exception ex)
            {
                _logger.AddException("[CreateWorkflowTool_ExecuteAsync__ArrayMappingException]", ex);
            // We'll continue with whatever we successfully mapped; array issues
            // should not kill the whole request.
            }

            // Validation � same as before.
            if (string.IsNullOrWhiteSpace(wf.Id))
            {
                errors.Add(new WorkflowAuthoringError { Field = "id", Message = "Workflow Id is required." });
            }

            if (string.IsNullOrWhiteSpace(wf.Name))
            {
                errors.Add(new WorkflowAuthoringError { Field = "name", Message = "Workflow Name is required." });
            }

            if (string.IsNullOrWhiteSpace(wf.Description))
            {
                errors.Add(new WorkflowAuthoringError { Field = "description", Message = "Workflow Description is required." });
            }

            // Ensure the Id is unique before attempting to create.
            if (!string.IsNullOrWhiteSpace(wf.Id))
            {
                try
                {
                    var inUse = await _workflowManager.QueryWorkflowIdInUseAsync(wf.Id, context?.Org);
                    if (inUse)
                    {
                        errors.Add(new WorkflowAuthoringError { Field = "id", Message = $"Workflow Id '{wf.Id}' is already in use." });
                    }
                }
                catch (Exception ex)
                {
                    _logger.AddException("[CreateWorkflowTool_ExecuteAsync__QueryIdException]", ex);
                    errors.Add(new WorkflowAuthoringError { Field = "id", Message = "Unable to verify whether the workflow Id is already in use." });
                }
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
                var result = await _workflowManager.AddWorkflowDefinitionAsync(wf, context?.Org, context?.User);
                var response = new WorkflowAuthoringResponse
                {
                    Ok = result.Successful,
                    Workflow = wf
                };
                if (!result.Successful)
                {
                    foreach (var err in result.Errors)
                    {
                        response.Errors.Add(new WorkflowAuthoringError { Field = err.ErrorCode, Message = err.Message });
                    }
                }
                else
                {
                    response.Messages.Add("Workflow created successfully.");
                }

                var json = JsonConvert.SerializeObject(response);
                return InvokeResult<string>.Create(json);
            }
            catch (Exception ex)
            {
                _logger.AddException("[CreateWorkflowTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("CreateWorkflowTool failed to process the request.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Create a new workflow definition. The input workflow object is validated and persisted; " + "the response contains ok/messages/errors and the resulting workflow.", p =>
            {
                p.Any("workflow", "object", "Workflow definition to create. At minimum, id, name, and description are required. " + "Other fields mirror the WorkflowDefinition model.", required: true);
            });
        }
    }
}