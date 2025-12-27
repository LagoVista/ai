using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using LagoVista.Core.Validation;
using LagoVista.Core;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        public const string ToolSummary = "used to create agent modes";
        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = "Create a new Agent Mode after confirming all required fields and selected tool IDs with the user.";
        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Create and persist a new AgentMode definition.", p =>
            {
                p.String("key", "Human-readable key, e.g. \"general\", \"ddr_authoring\". Must be unique.", required: true);
                p.String("display_name", "Display name for UI surfaces. May match key.", required: true);
                p.String("description", "Short description of what this mode is and covers.", required: true);
                p.String("when_to_use", "One-line \"when to use this mode\" summary for the Mode Catalog prompt.", required: true);
                p.String("welcome_message", "Optional welcome message shown when entering this mode.");
                p.Any("mode_instructions", "array", "DDR's that include mode-specific behavior instructions for the LLM when this mode is active.");
                p.Any("references", "array", "Reference Type DDRS that should be injected when this mode becomes active.");
                p.Any("behavior_hints", "array", "Optional structured hints like \"preferStructuredOutput\", \"avoidDestructiveTools\".");
                p.Any("human_role_hints", "array", "Hints about the human's role, e.g. \"The human is authoring DDRs\".");
                p.Any("associated_tool_ids", "array", "Tool IDs that are enabled when this mode is active.");
                p.Any("tool_group_hints", "array", "Optional grouping hints for tools, e.g. \"authoring\", \"diagnostics\".");
                p.Any("rag_scope_hints", "array", "RAG scoping hints, e.g. \"boost:DDR_DDRs\", \"exclude:telemetry\".");
                p.Any("strong_signals", "array", "Phrases strongly associated with this mode.");
                p.Any("weak_signals", "array", "Weaker hints that might lean toward this mode.");
                p.Any("example_utterances", "array", "Representative user utterances that clearly belong to this mode.");
                p.String("status", "\"active\", \"experimental\", or \"deprecated\".", enumValues: new[] { "active", "experimental", "deprecated" }, required: true);
                p.String("version", "Simple version string, e.g. \"v1\", \"v1.1\".", required: true);
                p.Boolean("is_default", "True if this is the default mode when no explicit mode is set.", required: true);
            });
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
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
                ReferenceDdrs = args["references"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                AgentInstructionDdrs = args["agent_instructions"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                BehaviorHints = args["behavior_hints"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                HumanRoleHints = args["human_role_hints"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                AssociatedToolIds = args["associated_tool_ids"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                ToolGroupHints = args["tool_group_hints"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                RagScopeHints = args["rag_scope_hints"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                StrongSignals = args["strong_signals"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                WeakSignals = args["weak_signals"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                ExampleUtterances = args["example_utterances"]?.ToObject<string[]>() ?? Array.Empty<string>(),
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