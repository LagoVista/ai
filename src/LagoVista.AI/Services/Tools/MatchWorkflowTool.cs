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
    /// Tool that attempts to match a free-form user message to one or more
    /// candidate workflows based on their UserIntentPatterns.
    ///
    /// The LLM uses this tool when it is unsure which workflow best fits a
    /// user's request and wants a ranked set of candidates.
    /// </summary>
    public sealed class MatchWorkflowTool : IAgentTool
    {
        private readonly IWorkflowDefinitionManager _workflowManager;
        private readonly IAdminLogger _logger;

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = "Use this tool when you have a user message and want to know which workflow(s) are most relevant. Supply the userMessage text and this tool returns candidate workflows with match scores.";

        public const string ToolName = "agent_workflows_match";

        public MatchWorkflowTool(IWorkflowDefinitionManager workflowManager, IAdminLogger logger)
        {
            _workflowManager = workflowManager ?? throw new ArgumentNullException(nameof(workflowManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private sealed class MatchWorkflowArgs
        {
            public string UserMessage { get; set; }
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
                    "MatchWorkflowTool requires a non-empty arguments object with 'userMessage'.");
            }

            MatchWorkflowArgs args;
            try
            {
                args = JsonConvert.DeserializeObject<MatchWorkflowArgs>(argumentsJson) ?? new MatchWorkflowArgs();
            }
            catch (Exception ex)
            {
                _logger.AddException("[MatchWorkflowTool_ExecuteAsync__DeserializeException]", ex);
                return InvokeResult<string>.FromError("MatchWorkflowTool could not deserialize argumentsJson.");
            }

            if (string.IsNullOrWhiteSpace(args.UserMessage))
            {
                return InvokeResult<string>.FromError(
                    "MatchWorkflowTool requires 'userMessage'.");
            }

            try
            {
                var userText = args.UserMessage.ToLowerInvariant();

                var listRequest = new ListRequest
                {
                    PageIndex = 0,
                    PageSize = 200
                };

                var defs = await _workflowManager.GetWorkflowDefinitionsAsync(listRequest, context.Org, context.User);

                var matches = defs.Model
                    .Where(wf => wf.Status != WorkflowStatus.Disabled)
                    .Select(wf =>
                    {
                        var patterns = wf.UserIntentPatterns ?? new System.Collections.Generic.List<string>();

                        var bestScore = 0.0;
                        foreach (var pattern in patterns)
                        {
                            if (string.IsNullOrWhiteSpace(pattern))
                                continue;

                            var patternLower = pattern.ToLowerInvariant();
                            if (userText.Contains(patternLower))
                            {
                                // Very simple scoring: exact containment -> 1.0
                                bestScore = 1.0;
                                break;
                            }
                        }

                        if (bestScore <= 0.0)
                        {
                            return (WorkflowMatch)null;
                        }

                        return new WorkflowMatch
                        {
                            WorkflowId = wf.Id,
                            Title = wf.Name,
                            Description = wf.Description,
                            MatchScore = bestScore,
                            Status = wf.Status,
                            Visibility = wf.Visibility
                        };
                    })
                    .Where(m => m != null)
                    .OrderByDescending(m => m.MatchScore)
                    .ToList();

                var response = new WorkflowRegistryMatchResponse
                {
                    Matches = matches
                };

                var json = JsonConvert.SerializeObject(response);
                return InvokeResult<string>.Create(json);
            }
            catch (Exception ex)
            {
                _logger.AddException("[MatchWorkflowTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("MatchWorkflowTool failed to process the request.");
            }
        }

        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Match a free-form user message to one or more candidate workflows based on their intent patterns.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        userMessage = new
                        {
                            type = "string",
                            description = "The raw user message text you want to match to known workflows."
                        }
                    },
                    required = new[] { "userMessage" }
                }
            };

            return schema;
        }
    }
}
