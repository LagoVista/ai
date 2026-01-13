using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Managers;
using LagoVista.AI.Models;
using LagoVista.AI.Models.AuthoritativeAnswers;
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

        public const string ToolUsageMetadata = @"
Purpose: Attempt to resolve a single, specific clarification question from authoritative stores. This tool does not ask the user and does not write data. When to call

Call when you need a clarification to proceed and you want to check whether it’s already settled.
Call before asking the human anything (policy handled elsewhere). Inputs (JSON)
orgId (string, required): Organization id (PartitionKey).
question (string, required): One precise question. Keep it singular (no multi-part).
tags (string[], optional): Symbol/type/property tokens to improve matching. Output Returns a JSON string that deserializes to AuthoritativeAnswerLookupResult:
status: Answered | NotFound | Conflict
answer: string (present when Answered)
sourceRef: string (e.g., aq:<id> or ddr:<id> if later added)
confidence: high | medium | low
conflicts: array of { aqId, answer, sourceRef, confidence } (present when Conflict) How to use the output
If status == Answered: use answer as authoritative and continue.
If status == NotFound: you still do not have an authoritative answer.
If status == Conflict: do not pick arbitrarily; you have multiple competing authoritative answers. Do / Don’t
Do: ask exactly one question per call.
Do: include tags when you know relevant symbols (e.g., OrgId, AgentSession, OwnerOrganization.Id).
Don’t: include multiple unrelated questions in one question string.
Don’t: fabricate an answer if NotFound or Conflict. Example call
json
{
  ""question"": ""How should OrgId be resolved when persisting AgentSession?"",
  ""tags"": [""OrgId"", ""AgentSession""]
}";


        private readonly IAuthoritativeAnswerManager _aqManager;
        private readonly IAdminLogger _logger;

        public AskAgentFirstTool(IAuthoritativeAnswerManager aqManager, IAdminLogger logger)
        {
            _aqManager = aqManager ?? throw new ArgumentNullException(nameof(aqManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public const string ToolSummary = "Ask a precise clarification question. Tool attempts to answer from authoritative sources only (no user prompting, no writes).";

        public bool IsToolFullyExecutedOnServer => true;


        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, ToolSummary, p =>
            {
                p.String("question", "Single, specific clarification question.", required: true);
                p.StringArray("tags", "Optional tags (symbols/types/properties)");
            });
        }


        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {  try
            {
                var args = Newtonsoft.Json.JsonConvert.DeserializeObject<AskAgentFirstArgs>(argumentsJson);
                if (args == null) return InvokeResult<string>.FromError("invalid_args");

                var result = await _aqManager.LookupAsync(context.Envelope.Org.Id, args.Question, args.Tags);
                return InvokeResult<string>.Create(Newtonsoft.Json.JsonConvert.SerializeObject(result));
            }
            catch (Exception ex)
            {
                _logger.AddException("[AskAgentFirstTool]", ex);
                return InvokeResult<string>.FromException("ask_agent_first_failed", ex);
            }
        }

        private class AskAgentFirstArgs
        {
            public string Question { get; set; }
            public List<string> Tags { get; set; }
        }
    }
}
