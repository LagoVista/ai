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

        public const string ToolSummary = "update an agent mode";
        public const string ToolName = "update_agent_mode";
        public const string ToolUsageMetadata = "Update an existing Agent Mode by Id after clarifying which fields the user wants to change.";
        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Update an existing AgentMode definition by Id.", p =>
            {
                p.String("id", "Canonical immutable AgentMode Id.", required: true);
                p.String("key", "Human-readable key, e.g. \"general\", \"ddr_authoring\".", required: true);
                p.String("display_name", "Display name for UI surfaces. May match key.", required: true);
                p.String("description", "Short description of what this mode is and covers.", required: true);
                p.String("when_to_use", "One-line \"when to use this mode\" summary.", required: true);
                p.String("welcome_message", "Optional welcome message shown when entering this mode.");
                p.Any("mode_instructions", "array", "");
                p.Any("behavior_hints", "array", "");
                p.Any("human_role_hints", "array", "");
                p.Any("associated_tool_ids", "array", "Tool IDs that are enabled when this mode is active.");
                p.Any("tool_group_hints", "array", "");
                p.Any("rag_scope_hints", "array", "");
                p.Any("strong_signals", "array", "");
                p.Any("weak_signals", "array", "");
                p.Any("example_utterances", "array", "");
                p.String("status", "\"active\", \"experimental\", or \"deprecated\".", enumValues: new[] { "active", "experimental", "deprecated" }, required: true);
                p.String("version", "Simple version string, e.g. \"v1\", \"v1.1\".", required: true);
                p.Boolean("is_default", "True if this is the default mode when no explicit mode is set.", required: true);
            });
        }

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
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
            existing.AgentInstructionDdrs = args["agent_instructions"]?.ToObject<string[]>() ?? Array.Empty<string>();
            existing.BehaviorHints = args["behavior_hints"]?.ToObject<string[]>() ?? Array.Empty<string>();
            existing.HumanRoleHints = args["human_role_hints"]?.ToObject<string[]>() ?? Array.Empty<string>();
            existing.AssociatedToolIds = args["associated_tool_ids"]?.ToObject<string[]>() ?? Array.Empty<string>();
            existing.RagScopeHints = args["rag_scope_hints"]?.ToObject<string[]>() ?? Array.Empty<string>();
            existing.StrongSignals = args["strong_signals"]?.ToObject<string[]>() ?? Array.Empty<string>();
            existing.WeakSignals = args["weak_signals"]?.ToObject<string[]>() ?? Array.Empty<string>();
            existing.ExampleUtterances = args["example_utterances"]?.ToObject<string[]>() ?? Array.Empty<string>();
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
                    version = summary.Version,
                    is_default = summary.IsDefault
                },
                summary
            };
            return InvokeResult<string>.Create(JObject.FromObject(jObj).ToString());
        }
    }
}