using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Models;
using LagoVista.AI.Models.AuthoritativeAnswers;
using LagoVista.Core;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Services.Tools
{
    public class WriteAuthoritativeAnswerTool : IAgentTool
    {
        public const string ToolName = "write_authoritative_answer";

        public string Name => ToolName;

        public const string ToolSummary = "Persist a human-provided answer into the Reference Library as a Reference Entry (writes).";

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = @"
Purpose
- Persist a human-provided answer into the Reference Library as a Reference Entry.
- This tool writes data.

When to call
- Call only when you have a settled answer from a human and the user explicitly wants it saved.

Required inputs (JSON)
Question targets (all required):
- humanQuestion (string): Human-friendly phrasing.
- modelQuestion (string): Unambiguous, action-oriented phrasing for a model.
- embedQuestion (string): Retrieval-optimized phrasing. Must include anchors (symbols/types/properties) when known.

Answer targets (all required):
- humanAnswer (string): Human-friendly answer (1-5 sentences).
- modelAnswer (string): Short, directly actionable answer for a model.

Optional inputs (JSON)
- appliesTo (string[]): Symbols/types/properties this applies to. If omitted or empty, the manager will infer.
- sourceRef (string): Source of the answer (e.g., human, ddr:SYS-000123, faq:<id>). Default: human.
- scope (string[]): Optional scope tags.

Validation rules
- All required strings must be non-empty.
- Provide exactly one question/answer set per call.

Notes
- The tool will map fields into a ReferenceEntry and call the ReferenceEntryManager to persist.
- PrimaryTla and ReferenceIdentifier are assigned by server-side logic you will add.
";

        private readonly IReferenceEntryManager _referenceEntryManager;
        private readonly IAdminLogger _logger;
        private readonly ISerialNumberManager _serialNumberManager;

        public WriteAuthoritativeAnswerTool(IReferenceEntryManager referenceEntryManager, IAdminLogger logger, ISerialNumberManager serialNumberManager)
        {
            _referenceEntryManager = referenceEntryManager ?? throw new ArgumentNullException(nameof(referenceEntryManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serialNumberManager = serialNumberManager ?? throw new ArgumentNullException(nameof(serialNumberManager));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, ToolSummary, p =>
            {
                // Questions (required)
                p.String("humanQuestion", "Human-friendly question.", required: true);
                p.String("modelQuestion", "Model-formatted question (unambiguous, action-oriented).", required: true);
                p.String("embedQuestion", "Embedding/retrieval formatted question. Include anchors when known.", required: true);

                // Answers (required)
                p.String("humanAnswer", "Human-friendly answer.", required: true);
                p.String("modelAnswer", "Model-optimized answer (short, actionable).", required: true);

                // Optional
                p.StringArray("appliesTo", "Optional applies-to tokens (symbols/types/properties). If omitted, manager will infer.");
                p.String("sourceRef", "Optional source of the answer (e.g., human, ddr:SYS-000123, faq:<id>). Default: human.");
                p.StringArray("scope", "Optional scope tags.");
            });
        }

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            try
            {
                var args = Newtonsoft.Json.JsonConvert.DeserializeObject<WriteReferenceEntryArgs>(argumentsJson);
                if (args == null) return InvokeResult<string>.FromError("invalid_args");

                var validationError = ValidateArgs(args);
                if (!String.IsNullOrEmpty(validationError))
                    return InvokeResult<string>.FromError(validationError);

                var sn = await _serialNumberManager.GenerateSerialNumber(context.Envelope.Org.Id, $"REFERENCEENTRY{context.Mode.ModeTla}REF");

                // Map tool args -> ReferenceEntry.
                // NOTE: PrimaryTla + ReferenceIdentifier will be assigned by your server-side logic.
                var entry = new ReferenceEntry
                {
                    Id = Guid.NewGuid().ToId(),
                    PrimaryTla = context.Mode.ModeTla,
                    ReferenceIdentifier = $"{context.Mode.ModeTla}-{sn:000000}",
                    HumanQuestion = args.HumanQuestion,
                    ModelQuestion = args.ModelQuestion,
                    EmbedQuestion = args.EmbedQuestion,

                    HumanAnswer = args.HumanAnswer,
                    ModelAnswer = args.ModelAnswer,

                    AppliesTo = args.AppliesTo ?? new List<string>(),
                    SourceRef = String.IsNullOrWhiteSpace(args.SourceRef) ? "human" : args.SourceRef.Trim(),
                    Scope = args.Scope ?? new List<string>(),

                    IsActive = true,

                    // Defaults (manager will also enforce if missing)
                    AnswerSource = EntityHeader<ReferenceEntrySource>.Create(ReferenceEntrySource.UserProvided),
                    AnswerConfidence = EntityHeader<ReferenceEntryConfidence>.Create(ReferenceEntryConfidence.High),
                    MetadataQuality = EntityHeader<ReferenceEntryMetadataQuality>.Create(ReferenceEntryMetadataQuality.Medium),
                };


                entry.CreationDate = context.TimeStamp;
                entry.LastUpdatedDate = context.TimeStamp;
                entry.CreatedBy = context.Envelope.User;
                entry.LastUpdatedBy = context.Envelope.User;
                entry.OwnerOrganization = context.Envelope.Org;
                entry.Key = entry.ReferenceIdentifier.ToLower().Replace("-", String.Empty);

                // Ensure org/user headers are passed through.
                var org = context.Envelope.Org;
                var user = context.Envelope.User;

                var result = await _referenceEntryManager.AddReferenceEntryAsync(entry, org, user);
                if (!result.Successful)
                    return InvokeResult<string>.FromInvokeResult(result);

                // Response payload: use document id + reference identifier (if assigned).
                var response = new
                {
                    status = "saved",
                    id = entry.Id,
                    referenceIdentifier = entry.ReferenceIdentifier,
                    sourceRef = $"ref:{entry.ReferenceIdentifier ?? entry.Id}"
                };

                return InvokeResult<string>.Create(Newtonsoft.Json.JsonConvert.SerializeObject(response));
            }
            catch(ValidationException invalidException)
            {
                var errorMessage = String.Join(',', invalidException.Errors);

                _logger.AddError(this.Tag(), $"Invalid data: {errorMessage}");

                return InvokeResult<string>.FromError($"Invalid data: {errorMessage}");
            }
            catch (Exception ex)
            {
                _logger.AddException(this.Tag(), ex);
                return InvokeResult<string>.FromException(this.Tag(), ex);
            }
        }

        private static string ValidateArgs(WriteReferenceEntryArgs args)
        {
            if (String.IsNullOrWhiteSpace(args.HumanQuestion)) return "humanQuestion is required";
            if (String.IsNullOrWhiteSpace(args.ModelQuestion)) return "modelQuestion is required";
            if (String.IsNullOrWhiteSpace(args.EmbedQuestion)) return "embedQuestion is required";
            if (String.IsNullOrWhiteSpace(args.HumanAnswer)) return "humanAnswer is required";
            if (String.IsNullOrWhiteSpace(args.ModelAnswer)) return "modelAnswer is required";

            // Basic sanity: keep it single-item oriented.
            if (args.HumanQuestion.Contains("\n\n") && args.HumanQuestion.Contains("?\n"))
                return "humanQuestion appears to contain multiple questions; provide exactly one";

            return null;
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }


        private class WriteReferenceEntryArgs
        {
            public string HumanQuestion { get; set; }
            public string ModelQuestion { get; set; }
            public string EmbedQuestion { get; set; }

            public string HumanAnswer { get; set; }
            public string ModelAnswer { get; set; }

            public List<string> AppliesTo { get; set; }
            public string SourceRef { get; set; }
            public List<string> Scope { get; set; }
        }
    }
}
