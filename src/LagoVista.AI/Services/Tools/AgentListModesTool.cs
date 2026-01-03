using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Read-only tool that returns a summary of all configured agent modes
    /// from the injected IAgentModeCatalogService.
    /// </summary>
    public sealed class AgentListModesTool : IAgentTool
    {
        private readonly IAdminLogger _logger;
        public const string ToolName = "agent_list_modes";
        public const string ToolUsageMetadata = @"Use this tool to list available agent modes and their high-level descriptions.
Call it when the user asks about modes, needs help choosing one, or before invoking the agent_change_mode tool.
Return Display Name (Key) with description.";

        public const string ToolSummary = "used to list agent modes that the user can choose from to customzie agent behavior";
        public AgentListModesTool(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        private sealed class ListModesArgs
        {
            public bool? IncludeExamples { get; set; }
        }

        private sealed class ModeDescriptor
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("key")]
            public string Key { get; set; }

            [JsonProperty("displayName")]
            public string DisplayName { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("systemPromptSummary")]
            public string SystemPromptSummary { get; set; }

            [JsonProperty("isDefault")]
            public bool IsDefault { get; set; }

            [JsonProperty("humanRoleHints")]
            public string[] HumanRoleHints { get; set; }

            [JsonProperty("exampleUtterances")]
            public string[] ExampleUtterances { get; set; }
        }

        private sealed class ListModesResult
        {
            [JsonProperty("modes")]
            public List<ModeDescriptor> Modes { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var args = string.IsNullOrWhiteSpace(argumentsJson) ? new ListModesArgs() : JsonConvert.DeserializeObject<ListModesArgs>(argumentsJson) ?? new ListModesArgs();
                var includeExamples = args.IncludeExamples.GetValueOrDefault(false);
                var result = new ListModesResult
                {
                    Modes = new List<ModeDescriptor>()
                };
                foreach (var mode in context.AgentContext.AgentModes.Select(md => md.CreateSummary()))
                {
                    if (mode == null)
                    {
                        continue;
                    }

                    var descriptor = new ModeDescriptor
                    {
                        Id = mode.Id ?? string.Empty,
                        Key = mode.Key ?? string.Empty,
                        DisplayName = mode.Name ?? string.Empty,
                        Description = mode.Description ?? string.Empty,
                        SystemPromptSummary = mode.SystemPromptSummary ?? string.Empty,
                        IsDefault = mode.IsDefault,
                        HumanRoleHints = mode.HumanRoleHints ?? Array.Empty<string>(),
                        ExampleUtterances = includeExamples ? (mode.ExampleUtterances ?? Array.Empty<string>()) : Array.Empty<string>()
                    };
                    result.Modes.Add(descriptor);
                }

                var json = JsonConvert.SerializeObject(result);
                return Task.FromResult(InvokeResult<string>.Create(json));
            }
            catch (Exception ex)
            {
                _logger.AddException("[AgentListModesTool_ExecuteAsync__Exception]", ex);
                return Task.FromResult(InvokeResult<string>.FromError("AgentListModesTool failed to list modes."));
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "List the available agent modes and their high-level descriptions. " + "Use this to explain mode options to the user or decide which mode might be appropriate.", p =>
            {
                p.Boolean("includeExamples", "If true, include example user utterances for each mode when available. " + "If false or omitted, examples may be omitted.");
            });
        }
    }
}