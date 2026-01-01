using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Imports a DDR from a Markdown document (create-only).
    ///
    /// v1: No chapters. Best-effort parsing. Enforces identifier < -> TLA/IDX consistency.
    /// If TLA-IDX already exists, reject and tell user what exists (TODO: wire DDR lookup).
    /// Approval fields are parsed when possible and should be confirmed by the user before being applied.
    /// </summary>
    public sealed class ImportDdrTool : IAgentTool
    {
        private readonly IAdminLogger _logger;
        private readonly IDdrManager _ddrManager;
        public const string ToolSummary = "used to import a ddr";
        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolName = "import_ddr";
        public const string ToolUsageMetadata = @"
You are assisting with the ingestion of a Detailed Design Review (DDR).

The provided Markdown DDR is the authoritative source of truth.
You MUST derive all requested outputs strictly from this Markdown DDR.
You MUST NOT invent rules, intent, scope, constraints, identifiers, types, status, or approvals that are not explicitly present in the Markdown DDR.

You MUST produce exactly ONE JSON object as output.
You MUST output only valid JSON. Do not wrap it in markdown code fences.
Do not include commentary, rationale, or explanations.

You MUST first determine the DDR identity and metadata by extracting it from the Markdown DDR.
You MUST extract and include these fields in the output JSON:
- ddrId
- title
- type
- status
- approvedBy
- approvalTimestamp
- ddrType (may not be present)

If any of these identity fields cannot be determined with confidence from the Markdown DDR, you MUST still include the field with a null value and set needsHumanConfirmation to true.

You MUST set needsHumanConfirmation to true if:
- Any identity field is null, or
- Any extracted identity field is ambiguous, or
- Any approval metadata appears missing or inconsistent with an Approved DDR.

You MUST then generate derived fields conditionally based on the extracted DDR type.


The DDR type MUST match exactly one of these values:
- Instruction
- Referential
- Generation
- Policy / Rules / Governance

If DDR Type is not present, you may try to infer it from the content, if you are reasonably sure you can just use this.  

if the DDR Type is ambigous or can't be deterined you must stop and confirm the DDR type before running the tool.

Derived field generation rules:

If type == ""Instruction"":
- You MUST include: humanSummary, condensedDdrContent, ragIndexCard, agentInstructions, referentialSummary

If type == ""Referential"":
- You MUST include: humanSummary, condensedDdrContent, ragIndexCard, agentInstructions, referentialSummary

If type == ""Generation"":
- You MUST include: humanSummary, condensedDdrContent, ragIndexCard
- You MUST NOT include: agentInstructions, referentialSummary

If type == ""Policy / Rules / Governance"":
- You MUST include: humanSummary, condensedDdrContent, ragIndexCard
- You MUST NOT include: agentInstructions, referentialSummary
- You MUST set needsHumanConfirmation to true

If needsHumanConfirmation is set to true
- You MUST also provide the reason why in needsHumanConfirmationReason.

Field constraints:

humanSummary:
- One to two full sentences for human readers
- Describes purpose and scope
- Must NOT contain procedural steps
- Must NOT introduce new rules

condensedDdrContent:
- Condensed reasoning substrate
- Must preserve all normative meaning
- May omit examples, historical context, and rationale
- Must NOT introduce new rules or interpretations

ragIndexCard:
- One to two sentences for retrieval and routing only
- MUST include verbatim: DDR ID, DDR Type, Status, Approval metadata, and a concise purpose statement
- MUST NOT contain normative rules
- MUST NOT contain normative keywords (MUST, MUST NOT, SHOULD, MAY)

referentialSummary (Referential only):
- Ultra-condensed awareness marker suitable for injection alongside many other referential summaries
- Must include the DDR ID verbatim
- Must be extremely short and token-efficient
- Must state what the DDR defines or governs (one short clause)
- Must indicate when the DDR becomes relevant (phase or trigger), using descriptive language only
- Must NOT contain procedural steps
- Must NOT contain normative keywords (MUST, MUST NOT, SHOULD, MAY)

agentInstructions (Instruction only):
- Executable procedural rules only
- Each instruction MUST begin with exactly one normative keyword: MUST, MUST NOT, SHOULD, or MAY
- Each instruction MUST contain exactly one normative keyword
- Ordering and gating semantics MUST be explicit
- Narrative explanation, rationale, and examples are forbidden

You MUST output the JSON object using exactly the following top-level shape.
You MUST NOT add any other top-level properties.

{
  ""ddrId"": string|null,
  ""title"": string|null,
  ""ddrType"": string|null,
  ""status"": string|null,
  ""approvedBy"": string|null,
  ""approvalTimestamp"": string|null,
  ""needsHumanConfirmation"": boolean,
  ""needsHumanConfirmationReason"": string,
  ""goal"": string,
  ""humanSummary"": string|null,
  ""condensedDdrContent"": string|null,
  ""ragIndexCard"": string|null,

  ""referentialSummary"": string|null,
  ""agentInstructions"": array|null
}

Additional rules:
- For any field that is forbidden for the DDR type, you MUST set it to null.
- agentInstructions MUST be an array of strings when present.
- Do not include markdown, headings, or bullet formatting outside of the strings themselves.
";
        public ImportDdrTool(IDdrManager ddrManager, IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ddrManager = ddrManager ?? throw new ArgumentNullException(nameof(ddrManager));
        }

        private sealed class ImportDdrArgs
        {
            // --- Authoritative input ---
            public string Markdown { get; set; }
            public string Source { get; set; }
            // --- Identity & metadata (extracted by LLM or tool, confirmed by human) ---
            public string DdrId { get; set; }
            public string Title { get; set; }

            public string Goal { get; set; }
            /// <summary>
            /// DDR type extracted from the Markdown.
            /// Must be exactly one of:
            /// Instruction, Referential, Generation, Policy / Rules / Governance
            /// </summary>
            public string DdrType { get; set; }
            public string Status { get; set; }
            public string ApprovedBy { get; set; }
            /// <summary>
            /// Approval timestamp as extracted from Markdown.
            /// Preserve raw value as written.
            /// </summary>
            public string ApprovalTimestamp { get; set; }
            /// <summary>
            /// Indicates whether extracted identity or generated fields require
            /// explicit human confirmation before persistence.
            /// </summary>
            public bool? NeedsHumanConfirmation { get; set; }

            /// <summary>
            /// If a human needs to review this this field must be provided why.
            /// </summary>
            public string NeedsHumanConfirmationReason { get; set; }

            // --- Derived fields (generated by LLM, validated by tool) ---
            /// <summary>
            /// Human-facing summary (1–2 sentences).
            /// Required for all DDR types.
            /// </summary>
            public string HumanSummary { get; set; }
            /// <summary>
            /// Condensed DDR content suitable as the default LLM reasoning substrate.
            /// Required for all DDR types.
            /// </summary>
            public string CondensedDdrContent { get; set; }
            /// <summary>
            /// RAG routing-only index card.
            /// Required for all DDR types.
            /// </summary>
            public string RagIndexCard { get; set; }
            /// <summary>
            /// Referential-only ultra-condensed awareness marker.
            /// Required only when Type == 'Referential'.
            /// Must be null for all other DDR types.
            /// </summary>
            public string ReferentialSummary { get; set; }
            /// <summary>
            /// Instruction-only executable ModeInstructionDdrs.
            /// Required only when Type == 'Instruction'.
            /// Must be null for all other DDR types.
            /// </summary>
            public string[] AgentInstructions { get; set; }
            // --- Control flags (tool-side orchestration) ---
            /// <summary>
            /// If true, do not persist anything; return extracted identity and generated
            /// fields for human review and confirmation.
            /// </summary>
            public bool? DryRun { get; set; }
            /// <summary>
            /// If true, indicates the human has confirmed extracted identity and generated
            /// fields and the tool may proceed to persist.
            /// </summary>
            public bool? Confirmed { get; set; }
        }

        private sealed class ApprovalParse
        {
            public string ApprovedBy { get; set; }
            public string ApprovalTimestampRaw { get; set; }
            public DateTimeOffset? ApprovalTimestamp { get; set; }
        }

        private sealed class ImportDdrParsed
        {
            public string Identifier { get; set; }
            public string Tla { get; set; }
            public int? Index { get; set; }
            public string Title { get; set; }
            public string Summary { get; set; }
            /// <summary>
            /// Free-form status value.
            /// </summary>
            public string Status { get; set; }
            public ApprovalParse Approval { get; set; }
            public string[] ParseWarnings { get; set; }
        }

        private sealed class ImportDdrResult
        {
            public bool Success { get; set; }
            public bool DryRun { get; set; }
            // Authoritative identity derived from Markdown parsing (hard validated)
            public string Identifier { get; set; }
            public string Tla { get; set; }
            public int? Index { get; set; }
            public string Title { get; set; }
            public string Status { get; set; }
            // Best-effort parse details (warnings, approval parse, etc.)
            public ImportDdrParsed Parsed { get; set; }
            // LLM-extracted identity + derived fields (for review and batch workflows)
            public ImportDdrGenerated Generated { get; set; }
            public string SessionId { get; set; }
        }

        private sealed class ImportDdrGenerated
        {
            // Identity as extracted by LLM (may be null/ambiguous)
            public string DdrId { get; set; }
            public string Title { get; set; }
            public string Goal { get; set; }
            public string Type { get; set; }
            public string Status { get; set; }
            public string ApprovedBy { get; set; }
            public string ApprovalTimestamp { get; set; }
            // Quarantine / review signal
            public bool? NeedsHumanConfirmation { get; set; }
            // Derived fields
            public string HumanSummary { get; set; }
            public string CondensedDdrContent { get; set; }
            public string RagIndexCard { get; set; }
            public string ReferentialSummary { get; set; }
            public string[] AgentInstructions { get; set; }
            // Optional: if you want quick operator insight during batch runs
            public string[] ValidationWarnings { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return InvokeResult<string>.FromError("import_ddr requires a non-empty arguments object.");
            }

            try
            {
                var args = JsonConvert.DeserializeObject<ImportDdrArgs>(argumentsJson) ?? new ImportDdrArgs();
                if (string.IsNullOrWhiteSpace(args.Markdown))
                {
                    return InvokeResult<string>.FromError("import_ddr requires 'markdown' containing the DDR content.");
                }

                var parsed = ParseMarkdown(args.Markdown);
                // Hard validation: enforce identifier/TLA/index from Markdown parsing.
                if (string.IsNullOrWhiteSpace(parsed.Identifier))
                {
                    return InvokeResult<string>.FromError("import_ddr could not parse 'identifier' from the Markdown.");
                }

                if (string.IsNullOrWhiteSpace(parsed.Tla) || !parsed.Index.HasValue)
                {
                    return InvokeResult<string>.FromError("import_ddr could not parse both 'tla' and 'index' from the Markdown.");
                }

                // Hard rule: identifier must match TLA/IDX (this should never happen)
                if (!TryParseIdentifier(parsed.Identifier, out var identTla, out var identIdx))
                {
                    return InvokeResult<string>.FromError($"import_ddr could not parse tla/index from identifier '{parsed.Identifier}'.");
                }

                if (!string.Equals(identTla, parsed.Tla, StringComparison.OrdinalIgnoreCase) || identIdx != parsed.Index.Value)
                {
                    return InvokeResult<string>.FromError($"import_ddr identifier mismatch: Markdown indicates {parsed.Tla}-{parsed.Index:000} but identifier is '{parsed.Identifier}' (parsed as {identTla}-{identIdx:000}).");
                }

                // Validate the LLM-extracted identity when present (soft: requires confirmation).
                // You still persist the authoritative identifier from parsed Markdown.
                var type = args.DdrType?.Trim();
                var allowedTypes = new[]
                {
                    "Instruction",
                    "Referential",
                    "Generation",
                    "Policy / Rules / Governance"
                };
                if (string.IsNullOrWhiteSpace(type))
                {
                    // Tool usage instructions say: type can be null but then needsHumanConfirmation must be true.
                    // We enforce that semantics here.
                    if (args.NeedsHumanConfirmation != true)
                    {
                        return InvokeResult<string>.FromError("import_ddr type is missing but needsHumanConfirmation is not true.");
                    }
                }
                else if (!allowedTypes.Contains(type, StringComparer.Ordinal))
                {
                    // Not an exact match => must require confirmation.
                    if (args.NeedsHumanConfirmation != true)
                    {
                        return InvokeResult<string>.FromError($"import_ddr type '{type}' is not one of the allowed values and needsHumanConfirmation is not true.");
                    }
                }

                // Common derived fields required for all types
                if (string.IsNullOrWhiteSpace(args.HumanSummary))
                {
                    return InvokeResult<string>.FromError("import_ddr did not create 'humanSummary'.");
                }

                if (string.IsNullOrWhiteSpace(args.CondensedDdrContent))
                {
                    return InvokeResult<string>.FromError("import_ddr did not create 'condensedDdrContent'.");
                }

                if (string.IsNullOrWhiteSpace(args.RagIndexCard))
                {
                    return InvokeResult<string>.FromError("import_ddr did not create 'ragIndexCard'.");
                }

                // Type-conditional fields
                if (string.Equals(type, "Instruction", StringComparison.Ordinal))
                {
                    if (args.AgentInstructions == null || args.AgentInstructions.Length == 0)
                    {
                        return InvokeResult<string>.FromError("import_ddr did not create 'agentInstructions' for an Instruction DDR.");
                    }

                    if (!string.IsNullOrWhiteSpace(args.ReferentialSummary))
                    {
                        return InvokeResult<string>.FromError("import_ddr must not include 'referentialSummary' for an Instruction DDR.");
                    }
                }
                else if (string.Equals(type, "Referential", StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(args.ReferentialSummary))
                    {
                        return InvokeResult<string>.FromError("import_ddr did not create 'referentialSummary' for a Referential DDR.");
                    }

                    if (args.AgentInstructions != null && args.AgentInstructions.Length > 0)
                    {
                        return InvokeResult<string>.FromError("import_ddr must not include 'agentInstructions' for a Referential DDR.");
                    }
                }
                else
                {
                    // Generation or Policy / Rules / Governance or unknown-but-confirmation-required
                    if (!string.IsNullOrWhiteSpace(args.ReferentialSummary))
                    {
                        return InvokeResult<string>.FromError($"import_ddr must not include 'referentialSummary' for this DDR type ({args.DdrType}).");
                    }

                    if (args.AgentInstructions != null && args.AgentInstructions.Length > 0)
                    {
                        return InvokeResult<string>.FromError($"import_ddr must not include 'agentInstructions' for this DDR type ({args.DdrType}).");
                    }

                    if (string.Equals(type, "Policy / Rules / Governance", StringComparison.Ordinal))
                    {
                        // Per ToolUsageInstructions: always requires confirmation.
                        if (args.NeedsHumanConfirmation != true)
                        {
                            return InvokeResult<string>.FromError("import_ddr Policy / Rules / Governance DDRs must set needsHumanConfirmation=true.");
                        }
                    }
                }

                var existingDdr = await _ddrManager.GetDdrByTlaIdentiferAsync(parsed.Identifier, context.Org, context.User, false);
                if (existingDdr != null)
                {
                    return InvokeResult<string>.FromError($"import_ddr - Failed DDR {parsed.Identifier} already exists as {existingDdr.Name}");
                }

                var dryRun = args.DryRun.GetValueOrDefault(false);
                if (dryRun || args.Confirmed != true)
                {
                    // Preview response includes both parsed (authoritative) and LLM-extracted fields.
                    var preview = new ImportDdrResult
                    {
                        Success = true,
                        DryRun = true,
                        Identifier = parsed.Identifier,
                        Tla = parsed.Tla,
                        Index = parsed.Index,
                        Title = parsed.Title,
                        Status = parsed.Status,
                        Parsed = parsed,
                        SessionId = context?.SessionId
                    };
                    return InvokeResult<string>.Create(JsonConvert.SerializeObject(preview));
                }

                // Confirmed path: create & persist
                var timeStamp = DateTime.UtcNow.ToJSONString();
                // Prefer parsed.Title if present, otherwise args.Title
                var finalTitle = parsed.Title ?? args.Title;
                // Prefer parsed.Status if present, otherwise args.Status
                var finalStatus = parsed.Status ?? args.Status;
                var ddr = new DetailedDesignReview
                {
                    DdrIdentifier = $"{parsed.Tla}-{parsed.Index.Value:000000}",
                    CreatedBy = context.User,
                    Key = parsed.Identifier.ToLower().Replace("-", String.Empty),
                    LastUpdatedBy = context.User,
                    CreationDate = timeStamp,
                    LastUpdatedDate = timeStamp,
                    OwnerOrganization = context.Org,
                    Goal = args.Goal,
                    Tla = parsed.Tla,
                    Index = parsed.Index.Value,
                    Name = finalTitle,
                    // New / revised fields (LLM-generated)
                    Type = type, // add this property to DetailedDesignReview
                    NeedsHumanConfirmation = args.NeedsHumanConfirmation == true, // add this property if you want to persist it
                    NeedsHumanConfirmationReason = args.NeedsHumanConfirmationReason,
                    HumanSummary = args.HumanSummary, // add if separate from SummaryInstructions; otherwise map to SummaryInstructions
                    CondensedDdrContent = args.CondensedDdrContent, // add
                    RagIndexCard = args.RagIndexCard, // add (replaces RagSummary)
                    ReferentialSummary = args.ReferentialSummary, // add
                    AgentInstructions = args.AgentInstructions == null ? null : string.Join("\n", args.AgentInstructions), // keep existing storage as string if needed
                    Status = finalStatus,
                    StatusTimestamp = timeStamp,
                    FullDDRMarkDown = args.Markdown
                };
                // Approval metadata: prefer parsed if parseable; otherwise accept args fields as raw strings if provided.
                if (parsed.Approval != null && parsed.Approval.ApprovalTimestamp.HasValue)
                {
                    ddr.ApprovedBy = context.User;
                    ddr.ApprovedTimestamp = parsed.Approval.ApprovalTimestampRaw;
                }
                else if (!string.IsNullOrWhiteSpace(args.ApprovalTimestamp) || !string.IsNullOrWhiteSpace(args.ApprovedBy))
                {
                    ddr.ApprovedBy = context.User;
                    ddr.ApprovedTimestamp = args.ApprovalTimestamp;
                }

                var addResult = await _ddrManager.AddDdrAsync(ddr, context.Org, context.User);
                if (!addResult.Successful)
                {
                    return InvokeResult<string>.FromError($"import_ddr failed to create DDR: {addResult.ErrorMessage}");
                }

                var result = new ImportDdrResult
                {
                    Success = true,
                    DryRun = false,
                    Identifier = parsed.Identifier,
                    Tla = parsed.Tla,
                    Index = parsed.Index,
                    Title = finalTitle,
                    Status = finalStatus,
                    Parsed = parsed,
                    SessionId = context?.SessionId
                };
                result.Generated = new ImportDdrGenerated
                {
                    DdrId = args.DdrId,
                    Title = args.Title,
                    Type = args.DdrType,
                    Status = args.Status,
                    ApprovedBy = args.ApprovedBy,
                    ApprovalTimestamp = args.ApprovalTimestamp,
                    NeedsHumanConfirmation = args.NeedsHumanConfirmation,
                    HumanSummary = args.HumanSummary,
                    CondensedDdrContent = args.CondensedDdrContent,
                    RagIndexCard = args.RagIndexCard,
                    ReferentialSummary = args.ReferentialSummary,
                    AgentInstructions = args.AgentInstructions
                };
                return InvokeResult<string>.Create(JsonConvert.SerializeObject(result));
            }
            catch (ValidationException vex)
            {
                _logger.AddException("[ImportDdrTool_ExecuteAsync__ValidationException]", vex);
                return InvokeResult<string>.FromError($"import_ddr failed validation problem(s): {String.Join(",", vex.Errors.Select(err => err))}");
            }
            catch (Exception ex)
            {
                _logger.AddException("[ImportDdrTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError($"import_ddr failed to process arguments: {ex.Message}.");
            }
        }

        private  ImportDdrParsed ParseMarkdown(string markdown)
        {
            // This is intentionally best-effort and geared toward the TUL-011 example.
            // Anything not found remains null and should be confirmed with user.
            var warnings = new System.Collections.Generic.List<string>();
            // Identifier line example: **ID:** TUL-011
            var identifier = MatchFirst(markdown, @"(?mi)^\*\*ID:\*\*\s*(?<v>[A-Za-z0-9_-]+)\s*$");
            if (string.IsNullOrWhiteSpace(identifier))
            {
                // Fallback: "ID: TUL-011" without bold
                identifier = MatchFirst(markdown, @"(?mi)^\s*ID:\s*(?<v>[A-Za-z0-9_-]+)\s*$");
            }

            // Title line example: **Title:** Agent Tool Contract
            var title = MatchFirst(markdown, @"(?mi)^\*\*Title:\*\*\s*(?<v>.+?)\s*$");
            if (string.IsNullOrWhiteSpace(title))
            {
                // Fallback: first H1 "# TUL-011 — Agent Tool Contract" => title after dash
                title = TryParseTitleFromH1(markdown);
            }

            // Status line example: **Status:** Approved
            var status = MatchFirst(markdown, @"(?mi)^\*\*Status:\*\*\s*(?<v>.+?)\s*$");
            // Optional summary: not present in TUL-011; keep best-effort.
            // If you standardize a SummaryInstructions field later, add it here.
            var summary = MatchFirst(markdown, @"(?mi)^\*\*Summary:\*\*\s*(?<v>.+?)\s*$");
            // Derive TLA + index from identifier
            string tla = null;
            int? idx = null;
            if (!string.IsNullOrWhiteSpace(identifier) && TryParseIdentifier(identifier, out var itla, out var iidx))
            {
                tla = itla;
                idx = iidx;
            }
            else if (!string.IsNullOrWhiteSpace(identifier))
            {
                warnings.Add($"Could not parse TLA/Index from identifier '{identifier}'. Expected format like TUL-011.");
            }

            // Approval parsing (best-effort)
            // - **Approved By:** Kevin Wolf
            // - **Approval Timestamp:** 2025-12-06 13:30:00 EST (UTC-05:00)
            var approvedBy = MatchFirst(markdown, @"(?mi)^\-\s*\*\*Approved By:\*\*\s*(?<v>.+?)\s*$");
            var approvalTimestampRaw = MatchFirst(markdown, @"(?mi)^\-\s*\*\*Approval Timestamp:\*\*\s*(?<v>.+?)\s*$");
            var approvalTimestamp = TryParseDateTimeOffsetLoose(approvalTimestampRaw);
            if (!string.IsNullOrWhiteSpace(approvalTimestampRaw) && approvalTimestamp == null)
            {
                warnings.Add($"Could not parse Approval Timestamp '{approvalTimestampRaw}' into a DateTimeOffset; keeping raw value only.");
            }

            return new ImportDdrParsed
            {
                Identifier = identifier,
                Tla = tla,
                Index = idx,
                Title = title,
                Summary = summary,
                Status = status,
                Approval = new ApprovalParse
                {
                    ApprovedBy = approvedBy,
                    ApprovalTimestampRaw = approvalTimestampRaw,
                    ApprovalTimestamp = approvalTimestamp
                },
                ParseWarnings = warnings.Count == 0 ? Array.Empty<string>() : warnings.ToArray()
            };
        }

        private static string MatchFirst(string text, string pattern)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            var m = Regex.Match(text, pattern);
            if (!m.Success)
                return null;
            var v = m.Groups["v"]?.Value;
            return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }

        private static string TryParseTitleFromH1(string markdown)
        {
            // Example: "# TUL-011 — Agent Tool Contract"
            var h1 = MatchFirst(markdown, @"(?mi)^\#\s*(?<v>.+?)\s*$");
            if (string.IsNullOrWhiteSpace(h1))
                return null;
            // If H1 contains " — " or " - ", attempt to strip leading identifier
            var parts = h1.Split(new[] { "—", "-" }, 2, StringSplitOptions.None).Select(p => p.Trim()).ToArray();
            if (parts.Length == 2 && parts[0].Length <= 16)
            {
                return parts[1].Trim();
            }

            return h1.Trim();
        }

        private bool TryParseIdentifier(string identifier, out string tla, out int index)
        {
            tla = null;
            index = 0;
            if (string.IsNullOrWhiteSpace(identifier))
            {
                _logger.AddCustomEvent(Core.PlatformSupport.LogLevel.Error, "[ImportDdrTool_TryParseIdentifier]", "Identifier not provided.");
                return false;
            }
            // Accept formats like TUL-011, TUL_011, TUL011 (last one optional if you want).
            var m = Regex.Match(identifier.Trim(), @"^(?<tla>[A-Za-z]{2,10})[-_](?<idx>\d{1,6})$");
            if (!m.Success)
            {
                _logger.AddCustomEvent(Core.PlatformSupport.LogLevel.Error, "[ImportDdrTool_TryParseIdentifier]", $"No match on identifier: {identifier}.");
                return false;
            }
            tla = m.Groups["tla"].Value.ToUpperInvariant();


            if (!int.TryParse(m.Groups["idx"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            {
                _logger.AddCustomEvent(Core.PlatformSupport.LogLevel.Error, "[ImportDdrTool_TryParseIdentifier]", $"Could not identify index: {identifier}.");
                return false;
            }

            return true;
        }

        private static DateTimeOffset? TryParseDateTimeOffsetLoose(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            // Try a few approaches; your example includes "EST (UTC-05:00)" which is not directly parseable.
            // We'll attempt to extract the "(UTC-05:00)" portion and parse with offset.
            // Example: "2025-12-06 13:30:00 EST (UTC-05:00)"
            var utcOffset = Regex.Match(value, @"\(\s*UTC(?<off>[+-]\d{2}:\d{2})\s*\)", RegexOptions.IgnoreCase);
            if (utcOffset.Success)
            {
                var off = utcOffset.Groups["off"].Value; // -05:00
                var dtPart = value.Substring(0, utcOffset.Index).Trim();
                // Remove trailing timezone token like "EST"
                dtPart = Regex.Replace(dtPart, @"\b[A-Z]{2,5}\b\s*$", "").Trim();
                if (DateTime.TryParse(dtPart, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
                {
                    if (TimeSpan.TryParse(off, CultureInfo.InvariantCulture, out var ts))
                    {
                        return new DateTimeOffset(dt, ts);
                    }
                }
            }

            // Fallback: try DateTimeOffset parse directly
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dto))
            {
                return dto;
            }

            // Fallback: DateTime parse => assume local
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dt2))
            {
                return new DateTimeOffset(dt2);
            }

            return null;
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Imports a DDR from a Markdown document (create-only). The DDR Markdown is authoritative. " + "The tool supports an LLM-assisted ingestion flow where identity may be extracted from Markdown and " + "derived fields are generated conditionally based on DDR type. The tool validates output and can " + "return parsed/generated values for human confirmation (dry-run) before persisting on confirmation.", p =>
            {
                p.String("markdown", "Full authoritative DDR Markdown content to import.", required: true);
                p.String("source", "Optional source label (e.g., filename/path/URL) for traceability.");
                p.Boolean("dryRun", "If true, do not persist anything; return extracted identity and generated derived fields for human review/confirmation.");
                p.Boolean("confirmed", "If true, indicates the human has confirmed extracted identity and generated fields and the tool may proceed to persist.");
                p.String("ddrId", "DDR identifier extracted from the Markdown (e.g., 'AGN-022'). If not determinable with confidence, set to null and set needsHumanConfirmation=true.");
                p.String("title", "DDR title extracted from the Markdown. If not determinable with confidence, set to null and set needsHumanConfirmation=true.");
                p.String("ddrType", "DDR type extracted from the Markdown. Must be exactly one of: 'Instruction', 'Referential', 'Generation', 'Policy / Rules / Governance'. " + "If ambiguous or not an exact match, set needsHumanConfirmation=true.");
                p.String("goal", "What was the goal as extracted from the Markdown, if one can not be extracted you can try to synthesize one from the markdown content, if the goal is not clear you must set needsHumanConfirmation=true");
                p.String("status", "DDR status extracted from the Markdown (e.g., 'Approved'). If missing or ambiguous, set to null and set needsHumanConfirmation=true.", required:true);
                p.String("approvedBy", "Approval 'Approved By' extracted from the Markdown Approval Metadata section. If missing or ambiguous, set to null and set needsHumanConfirmation=true.");
                p.String("approvalTimestamp", "Approval timestamp extracted from the Markdown Approval Metadata section. Preserve the raw value as written. If missing or ambiguous, set to null and set needsHumanConfirmation=true.");
                p.Boolean("needsHumanConfirmation", "True if any extracted identity field is null/ambiguous, approval metadata is missing/inconsistent, or the DDR type is not an exact allowed value. " + "When true, the tool must surface values for human confirmation before persisting.");
                p.String( "needsHumanConfirmationReaons", "If needsHumanConfirmation is set to true, this field contains the reason why.");
                p.String("humanSummary", "Human-facing summary (1–2 full sentences) describing purpose and scope. Must not include procedural steps and must not introduce new rules. Required for all DDR types.", required: true);
                p.String("condensedDdrContent", "Condensed DDR content suitable as the default LLM reasoning substrate after identification. Must preserve all normative meaning; may omit examples/rationale/history; must not introduce new rules or interpretations. Required for all DDR types.", required: true);
                p.String("ragIndexCard", "RAG routing-only index card (1–2 sentences). Must include DDR ID, DDR Type, Status, Approval metadata, and a concise purpose statement. Must not contain normative rules or normative keywords (MUST, MUST NOT, SHOULD, MAY). Required for all DDR types.", required: true);
                p.String("referentialSummary", "Referential-only ultra-condensed awareness marker intended for injection alongside many other referential summaries. Must include DDR ID, be extremely short, and must not include normative keywords or procedural steps. " + "Required only when ddrType == 'Referential'; must be null for all other types.");
                p.StringArray("agentInstructions", "Instruction-only executable ModeInstructions. Must be an array of strings. Each instruction must begin with exactly one normative keyword (MUST, MUST NOT, SHOULD, MAY) and contain exactly one normative keyword. " + "Must preserve ordering and gating semantics; must not include narrative explanation. Required only when ddrType == 'Instruction'; must be null for all other types.");
            });
        }
    }
}