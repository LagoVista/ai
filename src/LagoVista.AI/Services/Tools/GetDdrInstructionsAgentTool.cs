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
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Agent tool that retrieves full DDR details, including chapters.
    /// Tool name: "get_ddr".
    /// </summary>
    public class GetdDdrInstructionsAgentTool : IAgentTool
    {
        private IDdrManager _ddrManager;
        private IAdminLogger _adminLogger;

        public const string ToolName = "get_ddr_instructions";
        public GetdDdrInstructionsAgentTool(IDdrManager ddrManager, IAdminLogger adminLogger)
        {
            _ddrManager = ddrManager ?? throw new ArgumentNullException(nameof(ddrManager));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public const string ToolUsageMetadata = @"Request a ddr by identifier to return authortaive insstructoins as defined by the ddr";
        public string Name => ToolName;

        public const string ToolSummary = "gets a ddr and returns the Agent Instructions.";
        protected string Tag => $"[{nameof(GetdDdrInstructionsAgentTool)}]";

        public bool IsToolFullyExecutedOnServer => true;

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, ToolUsageMetadata, p =>
            {
                p.String("identifier", "DDR identifier in TLA-###### format, for example 'SYS-000001'.", required: true);
            });
        }

        public Task<InvokeResult<string>> ExecuteAsync(string payload, AgentToolExecutionContext context, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        
        {
            var baseTag = $"[{Tag}__Execute]";

            if (context == null)
            {
                return InvokeResult<string>.FromError("Execution context must not be null.");
            }


            var payload = JObject.Parse(argumentsJson);


            var identifier = payload.Value<string>("identifier")?.Trim();
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return InvokeResult<string>.Create($"{baseTag} identifier is required.");
            }

            try
            {
                var ddr = await _ddrManager.GetDdrByTlaIdentiferAsync(identifier, context.Envelope.Org, context.Envelope.User);
                if (ddr == null)
                {
                    return InvokeResult<string>.FromError($"{identifier} was not found");
                }

                if(String.IsNullOrEmpty(ddr.AgentInstructions))
                {
                    return InvokeResult<string>.FromError($"ddr does not include agent instructions");
                }

                var envelope = new JObject
                {
                    ["ok"] = true,
                    ["result"] = ddr.AgentInstructions
                };
                var json = envelope.ToString(Formatting.None);
                return InvokeResult<string>.Create(json);
            }
            catch (Exception ex)
            {
                _adminLogger.AddException(baseTag, ex);
                return InvokeResult<string>.FromException(baseTag, ex);
            }
        }
    }
}