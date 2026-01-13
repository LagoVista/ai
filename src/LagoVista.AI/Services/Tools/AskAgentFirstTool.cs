using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Services.Tools
{
    public class AskAgentFirstTool : IAgentTool
    {
        public const string ToolName = "ask_agent_first";

        public string Name => ToolName;

        public const string ToolSummary = "Ask a precise clarification question. Tool attempts to answer from Reference Entries only (no user prompting, no writes).";

        public const string ToolUsageMetadata = @"
Purpose
- Attempt to resolve a single, specific clarification question from Reference Entries.
- This tool does not ask the user and does not write data.

When to call
- Call when you are blocked and need a clarification to proceed.
- Call before asking the human anything.

Required inputs (JSON)
- modelQuestion (string): The question formatted for model consumption. Must be a single, specific question.

Optional inputs (JSON)
- tags (string[]): Optional symbol/type/property tokens. Used only as hints.

Output
Returns a JSON string that deserializes to AuthoritativeAnswerLookupResult:
- status: Answered | NotFound | Conflict
- answer: string (present when Answered)
- sourceRef: string (e.g., ref:<ReferenceIdentifier> or ddr:<id>)
- confidence: high | medium | low | unknown
- conflicts: array of { aqId, answer, sourceRef, confidence } (present when Conflict)

Rules
- Ask exactly one question per call.
- Do not include multiple unrelated questions in one call.
- If status is NotFound or Conflict, do not fabricate an answer.
";

        private readonly IReferenceEntryManager _referenceEntryManager;
        private readonly IAdminLogger _logger;

        public AskAgentFirstTool(IReferenceEntryManager referenceEntryManager, IAdminLogger logger)
        {
            _referenceEntryManager = referenceEntryManager ?? throw new ArgumentNullException(nameof(referenceEntryManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsToolFullyExecutedOnServer => true;

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, ToolSummary, p =>
            {
                p.String("modelQuestion", "The question formatted for model consumption. Must be a single, specific question.", required: true);
                p.StringArray("tags", "Optional tags (symbols/types/properties)");
            });
        }

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            try
            {
                var args = Newtonsoft.Json.JsonConvert.DeserializeObject<AskAgentFirstArgs>(argumentsJson);
                if (args == null) return InvokeResult<string>.FromError("invalid_args");
                if (String.IsNullOrWhiteSpace(args.ModelQuestion)) return InvokeResult<string>.FromError("modelQuestion is required");

                var result = await _referenceEntryManager.LookupAsync(context.Envelope.Org.Id, args.ModelQuestion);
                return InvokeResult<string>.Create(Newtonsoft.Json.JsonConvert.SerializeObject(result));
            }
            catch (Exception ex)
            {
                _logger.AddException("[AskAgentFirstTool]", ex);
                return InvokeResult<string>.FromException("ask_agent_first_failed", ex);
            }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }


        private class AskAgentFirstArgs
        {
            public string ModelQuestion { get; set; }
            public List<string> Tags { get; set; }
        }
    }
}
