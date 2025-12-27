using LagoVista.AI.Interfaces;
using Newtonsoft.Json.Linq;
using OpenTelemetry.Trace;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.AI.Managers;

namespace LagoVista.AI.Services.Tools
{
    public sealed class UpdateAgentModeTool : IAgentTool
    {
        private readonly IAgentContextManager _agentContextManager;
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);

        public UpdateAgentModeTool(IAgentContextManager agentContextManager)
        {
            _agentContextManager = agentContextManager;
        }


        public const string ToolSummary = "udpate an agent mode";

        public const string ToolName = "update_agent_mode";

        public const string ToolUsageMetadata = "Update an existing Agent Mode by Id after clarifying which fields the user wants to change.";

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public static object GetSchema()
        {
            var availableToolIds = AgentToolRegistry.Instance.GetAllToolIds();

            return  new
            {
                type = "function",
                name = ToolName,
                description = "Update an existing AgentMode definition by Id.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new
                        {
                            type = "string",
                            description = "Canonical immutable AgentMode Id."
                        },
                        key = new
                        {
                            type = "string",
                            description = "Human-readable key, e.g. \"general\", \"ddr_authoring\"."
                        },
                        display_name = new
                        {
                            type = "string",
                            description = "Display name for UI surfaces. May match key."
                        },
                        description = new
                        {
                            type = "string",
                            description = "Short description of what this mode is and covers."
                        },
                        when_to_use = new
                        {
                            type = "string",
                            description = "One-line \"when to use this mode\" summary."
                        },
                        welcome_message = new
                        {
                            type = "string",
                            description = "Optional welcome message shown when entering this mode.",
                            nullable = true
                        },
                        mode_instructions = new
                        {
                            type = "array",
                            items = new { type = "string" }
                        },
                        behavior_hints = new
                        {
                            type = "array",
                            items = new { type = "string" }
                        },
                        human_role_hints = new
                        {
                            type = "array",
                            items = new { type = "string" }
                        },
                        associated_tool_ids = new
                        {
                            type = "array",
                            description = "Tool IDs that are enabled when this mode is active.",
                            items = new
                            {
                                type = "string",
                            @enum = availableToolIds
    }
},
                    tool_group_hints = new
                    {
                        type = "array",
                        items = new { type = "string" }
                    },
                    rag_scope_hints = new
                    {
                        type = "array",
                        items = new { type = "string" }
                    },
                    strong_signals = new
                    {
                        type = "array",
                        items = new { type = "string" }
                    },
                    weak_signals = new
                    {
                        type = "array",
                        items = new { type = "string" }
                    },
                    example_utterances = new
                    {
                        type = "array",
                        items = new { type = "string" }
                    },
                    status = new
                    {
                        type = "string",
                        description = "\"active\", \"experimental\", or \"deprecated\".",
                        @enum = new[] { "active", "experimental", "deprecated" }
                    },
                    version = new
                    {
                        type = "string",
                        description = "Simple version string, e.g. \"v1\", \"v1.1\"."
                    },
                    is_default = new
                    {
                        type = "boolean",
                        description = "True if this is the default mode when no explicit mode is set."
                    }
                },
                required = new[]
                {
                    "id",
                    "key",
                    "display_name",
                    "description",
                    "when_to_use",
                    "status",
                    "version",
                    "is_default"
                }
            }
        };
    }

        public async Task<InvokeResult<string>> ExecuteAsync(
              string argumentsJson,
              AgentToolExecutionContext context,
              CancellationToken cancellationToken = default)
        {

            var args = JObject.Parse(argumentsJson);

            var id = args.Value<string>("id");
            if (string.IsNullOrWhiteSpace(id))
            {
                return InvokeResult<string>.FromError("id is required.");
            }

            var agentContext = await _agentContextManager.GetAgentContextAsync(context.AgentContext.Id, context.Org, context.User);
            var existing = agentContext.AgentModes.SingleOrDefault(md => md.Id == id);
            if (existing == null)
            {
                return InvokeResult<string>.FromError($"AgentMode with id '{id}' not found.");
            } 

    existing.Key = args.Value<string>("key")!;
    existing.DisplayName = args.Value<string>("display_name")!;
    existing.Description = args.Value<string>("description")!;
    existing.WhenToUse = args.Value<string>("when_to_use")!;
    existing.WelcomeMessage = args.Value<string>("welcome_message");

    existing.AgentInstructionDdrs =
        args["agent_instructions"]?.ToObject<string[]>() ?? Array.Empty<string>();
    existing.BehaviorHints =
        args["behavior_hints"]?.ToObject<string[]>() ?? Array.Empty<string>();
    existing.HumanRoleHints =
        args["human_role_hints"]?.ToObject<string[]>() ?? Array.Empty<string>();

    existing.AssociatedToolIds =
        args["associated_tool_ids"]?.ToObject<string[]>() ?? Array.Empty<string>();
    existing.ToolGroupHints =
        args["tool_group_hints"]?.ToObject<string[]>() ?? Array.Empty<string>();
    existing.RagScopeHints =
        args["rag_scope_hints"]?.ToObject<string[]>() ?? Array.Empty<string>();

    existing.StrongSignals =
        args["strong_signals"]?.ToObject<string[]>() ?? Array.Empty<string>();
    existing.WeakSignals =
        args["weak_signals"]?.ToObject<string[]>() ?? Array.Empty<string>();
    existing.ExampleUtterances =
        args["example_utterances"]?.ToObject<string[]>() ?? Array.Empty<string>();

    existing.Status = args.Value<string>("status")!;
    existing.Version = args.Value<string>("version")!;
    existing.IsDefault = args.Value<bool?>("is_default") ?? existing.IsDefault;


            await _agentContextManager.UpdateAgentContextAsync(agentContext, context.Org, context.User);
            var summary = existing.CreateSummary();

            var jObj = new
            {
                status = "ok",
                mode = new
                {
                    id = summary.Id,
                    key = summary.Key,
                    display_name = summary.DisplayName,
                    description = summary.Description,
                    when_to_use = summary.WhenToUse,
                    status = summary.Status,
                    version = summary.Version,
                    is_default = summary.IsDefault
                },
                summary
            };

            return InvokeResult<string>.Create(JObject.FromObject(jObj).ToString());

        }
    }

}
