using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Managers;
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
    public class WriteAuthoritativeAnswerTool : IAgentTool
    {
        public const string ToolName = "write_authoritative_answer";

        public const string ToolUsageMetadata = 
@"Purpose: Persist a human-provided authoritative answer into the AQ repository. This tool writes data. When to call

Call only when you have:
the original question, and
a human-provided answer that should be stored as authoritative. Inputs (JSON)
orgId (string, required): Organization id (PartitionKey).
question (string, required): The question being answered.
answer (string, required): The authoritative answer to store.
tags (string[], optional): Symbol/type/property tokens.
confidence (string, optional): high | medium | low (default high). Output Returns a JSON string like:
status: ""saved""
aqId: string (globally unique id)
sourceRef: string (aq:<aqId>) Do / Don’t
Do: store concise, reusable answers; prefer the “smallest useful snippet.”
Do: include tags for key symbols to improve future retrieval.
Don’t: store long architectural commitments (those belong elsewhere).
Don’t: store content approaching system limits; if it’s huge, it likely shouldn’t be AQ. Example call
json
{
  ""question"": ""How should OrgId be resolved when persisting AgentSession?"",
  ""answer"": ""Use OwnerOrganization.Id from EntityBase."",
  ""tags"": [""OrgId"", ""AgentSession"", ""OwnerOrganization.Id""],
  ""confidence"": ""high""
}"; 

        private readonly IAuthoritativeAnswerManager _aqManager;
        private readonly IAdminLogger _logger;

        public WriteAuthoritativeAnswerTool(IAuthoritativeAnswerManager aqManager, IAdminLogger logger)
        {
            _aqManager = aqManager ?? throw new ArgumentNullException(nameof(aqManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        public string Name => ToolName;


        public const string ToolSummary  = "Persist a human-provided authoritative answer into AQ (writes).";

        public bool IsToolFullyExecutedOnServer => true;


        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, ToolSummary, p =>
            {
                p.String("question", "Origianl Question.", required: true);
                p.String("answer", "Human-provided answer.", required: true);
                p.StringArray("tags", "Optional tags (symbols/types/properties)");
            });
        }


        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        { try
            {
                var args = Newtonsoft.Json.JsonConvert.DeserializeObject<WriteAuthoritativeAnswerArgs>(argumentsJson);
                if (args == null) return InvokeResult<string>.FromError("invalid_args");

                var saved = await _aqManager.SaveAsync(context.Envelope.Org.Id, args.Question, args.Answer, args.Tags, args.Confidence ?? "high");

                var response = new
                {
                    status = "saved",
                    aqId = saved.AqId,
                    sourceRef = $"aq:{saved.AqId}"
                };

                return InvokeResult<string>.Create(Newtonsoft.Json.JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                _logger.AddException("[WriteAuthoritativeAnswerTool]", ex);
                return InvokeResult<string>.FromException("write_authoritative_answer_failed", ex);
            }
        }

        private class WriteAuthoritativeAnswerArgs
        {
            public string Question { get; set; }
            public string Answer { get; set; }
            public List<string> Tags { get; set; }
            public string Confidence { get; set; }
        }
    }
}
