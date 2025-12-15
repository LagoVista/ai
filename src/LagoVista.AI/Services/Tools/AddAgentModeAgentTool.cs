using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using LagoVista.Core.Validation;
using System.Linq;
using LagoVista.Core;

namespace LagoVista.AI.Services.Tools
{
    public sealed class AddAgentModeTool : IAgentTool
    {
        private readonly IAgentContextManager _agentContextManager;

        public AddAgentModeTool(IAgentContextManager agentContextManager)
        {
            _agentContextManager = agentContextManager;
        }

        public const string ToolName = "add_agent_mode";

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = "Create a new Agent Mode after confirming all required fields and selected tool IDs with the user.";

        public static object GetSchema()
        {
            var availableToolIds = AgentToolRegistry.Instance.GetAllToolIds();

            return new
            {
                type = "function",
                name = ToolName,
                description = "Create and persist a new AgentMode definition.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        key = new
                        {
                            type = "string",
                            description = "Human-readable key, e.g. \"general\", \"ddr_authoring\". Must be unique."
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
                            description = "One-line \"when to use this mode\" summary for the Mode Catalog prompt."
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
                            description = "Mode-specific behavior instructions for the LLM when this mode is active.",
                            items = new { type = "string" }
                        },
                        behavior_hints = new
                        {
                            type = "array",
                            description = "Optional structured hints like \"preferStructuredOutput\", \"avoidDestructiveTools\".",
                            items = new { type = "string" }
                        },
                        human_role_hints = new
                        {
                            type = "array",
                            description = "Hints about the human's role, e.g. \"The human is authoring DDRs\".",
                            items = new { type = "string" }
                        },
                        associated_tool_ids = new
                        {
                            type = "array",
                            description = "Tool IDs that are enabled when this mode is active.",
                            items = new
                            {
                                type = "string",
                                // Finite option set – your multi-select.
                                @enum = availableToolIds
                            }
                        },
                        tool_group_hints = new
                        {
                            type = "array",
                            description = "Optional grouping hints for tools, e.g. \"authoring\", \"diagnostics\".",
                            items = new { type = "string" }
                        },
                        rag_scope_hints = new
                        {
                            type = "array",
                            description = "RAG scoping hints, e.g. \"boost:DDR_DDRs\", \"exclude:telemetry\".",
                            items = new { type = "string" }
                        },
                        strong_signals = new
                        {
                            type = "array",
                            description = "Phrases strongly associated with this mode.",
                            items = new { type = "string" }
                        },
                        weak_signals = new
                        {
                            type = "array",
                            description = "Weaker hints that might lean toward this mode.",
                            items = new { type = "string" }
                        },
                        example_utterances = new
                        {
                            type = "array",
                            description = "Representative user utterances that clearly belong to this mode.",
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

            // Server owns Id – ignore any Id sent from the LLM.
            var mode = new AgentMode
            {
                Id = Guid.NewGuid().ToId(), // hyphens removed
                Key = args.Value<string>("key")!,
                DisplayName = args.Value<string>("display_name")!,
                Description = args.Value<string>("description")!,
                WhenToUse = args.Value<string>("when_to_use")!,
                WelcomeMessage = args.Value<string>("welcome_message"),

                ModeInstructions =
                    args["mode_instructions"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                BehaviorHints =
                    args["behavior_hints"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                HumanRoleHints =
                    args["human_role_hints"]?.ToObject<string[]>() ?? Array.Empty<string>(),

                AssociatedToolIds =
                    args["associated_tool_ids"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                ToolGroupHints =
                    args["tool_group_hints"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                RagScopeHints =
                    args["rag_scope_hints"]?.ToObject<string[]>() ?? Array.Empty<string>(),

                StrongSignals =
                    args["strong_signals"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                WeakSignals =
                    args["weak_signals"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                ExampleUtterances =
                    args["example_utterances"]?.ToObject<string[]>() ?? Array.Empty<string>(),

                Status = args.Value<string>("status")!,
                Version = args.Value<string>("version")!,
                IsDefault = args.Value<bool?>("is_default") ?? false
            };

            var saved = await _agentContextManager.AddAgentModeAsync(context.AgentContext.Id, mode, context.Org, context.User);
            var summary = mode.CreateSummary();

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