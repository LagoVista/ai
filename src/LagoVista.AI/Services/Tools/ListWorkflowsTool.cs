using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Tool that returns a catalog of available workflows for the LLM.
    ///
    /// This is a read-only listing tool. It does not execute workflows.
    /// </summary>
    public sealed class ListWorkflowsTool : IAgentTool
    {
        private readonly IWorkflowDefinitionManager _workflowManager;
        private readonly IAdminLogger _logger;

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = "Use this tool when you need a list of available workflows. It returns a catalog of workflows (id, name, description, status, visibility, version) for the LLM to choose from. This tool never executes workflows directly.";

        public const string ToolName = "agent_workflows_list";

        public ListWorkflowsTool(IWorkflowDefinitionManager workflowManager, IAdminLogger logger)
        {
            _workflowManager = workflowManager ?? throw new ArgumentNullException(nameof(workflowManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private sealed class ListWorkflowsArgs
        {
            // Reserved for future filters (status, visibility, etc.).
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);

        public async Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Deserialize even if empty to keep the pattern consistent and future-proof.
                if (!string.IsNullOrWhiteSpace(argumentsJson))
                {
                    JsonConvert.DeserializeObject<ListWorkflowsArgs>(argumentsJson ?? "{}");
                }

                var listRequest = new ListRequest
                {
                    PageIndex = 0,
                    PageSize = 200
                };

                // Org/user can be threaded from context later; for now null is acceptable.
                var defs = await _workflowManager.GetWorkflowDefinitionsAsync(listRequest, context.Org, context.User);

                var items = defs.Model
                    .Where(wf => wf.Status != WorkflowStatus.Disabled)
                    .Select(wf => new WorkflowCatalogItem
                    {
                        WorkflowId = wf.Id,
                        Title = wf.Name,
                        Description = wf.Description,
                        Status = wf.Status,
                        Visibility = wf.Visibility,
                        Version = wf.Version
                    })
                    .ToList();

                var response = new WorkflowRegistryCatalogResponse
                {
                    Workflows = items
                };

                var json = JsonConvert.SerializeObject(response);
                return InvokeResult<string>.Create(json);
            }
            catch (Exception ex)
            {
                _logger.AddException("[ListWorkflowsTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("ListWorkflowsTool failed to process the request.");
            }
        }

        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "List all available workflows for the agent, excluding disabled ones. Returns a catalog for the LLM to choose from.",
                parameters = new
                {
                    type = "object",
                    properties = new { },
                    required = new string[] { }
                }
            };

            return schema;
        }
    }
}
