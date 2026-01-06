using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Imports a DDR from a Markdown document (create-only).
    ///
    /// v2: SYS-000010 schema-first parsing with compatibility fallback.
    /// - Markdown is authoritative.
    /// - Parses # Metadata / # Body / # Approval blocks; strict field labels when present.
    /// - Compatibility mode: legacy patterns supported; schema violations become warnings that require confirmation.
    /// - Creates chapters from Body headings and Summary lines.
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
You are ingesting a Detailed Design Review (DDR) from Markdown. The Markdown is the source of truth.

OUTPUT RULES
- Output exactly ONE valid JSON object (no markdown fences, no commentary).
- Do NOT invent identifiers, types, status, approvals, rules, or constraints not present in the Markdown.
- If a required value cannot be extracted with confidence, set it to null and set needsHumanConfirmation=true with a reason.

EXTRACT (from Markdown)
Populate these fields (null if unknown/ambiguous):
- ddrId
- title
- ddrType
- status
- approvedBy (from '# Approval' -> 'Approver:')
- approvalTimestamp (from '# Approval' -> 'Approval Timestamp:')

DDR TYPE (must be exact)
Allowed values (exact match):
- Instruction
- Referential
- Generation
- Policy / Rules / Governance
If missing/ambiguous/not an exact match: set ddrType=null and needsHumanConfirmation=true.

CONFIRMATION
Set needsHumanConfirmation=true if:
- Any extracted identity field is null/ambiguous, OR
- Status is Approved but approval fields are missing/inconsistent, OR
- ddrType is Policy / Rules / Governance.
When needsHumanConfirmation=true include needsHumanConfirmationReason.

DERIVED FIELDS (generate from Markdown; do not add new rules)
Always required:
- humanSummary (1–2 sentences; purpose/scope only; no steps)
- condensedDdrContent (condensed; preserve normative meaning; no new rules)
- ragIndexCard (1–2 sentences; include DDR ID, DDR Type, Status, approval metadata, and purpose; MUST NOT contain MUST/MUST NOT/SHOULD/MAY)

Model Generated Fields (generate from Markdown, do not add new reuls)
Required for - DDR Tyeps: Instruction, Referential
- agentInstructions: array of strings, Each string begins with exactly one of: MUST, MUST NOT, SHOULD, MAY, Each string contains exactly one normative keyword
- referentialSummary: ultra-short, includes DDR ID; no MUST/MUST NOT/SHOULD/MAY; no steps

TOP-LEVEL JSON SHAPE
Return a single JSON object with exactly these fields:
ddrId, title, ddrType, status, approvedBy, approvalTimestamp,
needsHumanConfirmation, needsHumanConfirmationReason,
goal, humanSummary, condensedDdrContent, ragIndexCard,
referentialSummary, agentInstructions,
dryRun, confirmed, markdown, source
";

        public ImportDdrTool(IDdrManager ddrManager, IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ddrManager = ddrManager ?? throw new ArgumentNullException(nameof(ddrManager));
        }

        private static readonly string[] AllowedTypes =
        {
            "Instruction",
            "Referential",
            "Generation",
            "Policy / Rules / Governance"
        };

        private static readonly string[] AllowedStatuses =
        {
            "Draft",
            "In Review",
            "Approved",
            "Tabled",
            "Cancelled",
            "Superseded"
        };

        private sealed class ImportDdrArgs
        {
            public string Markdown { get; set; }
            public string Source { get; set; }

            public string DdrId { get; set; }
            public string Title { get; set; }
            public string Goal { get; set; }
            public string DdrType { get; set; }
            public string Status { get; set; }
            public string ApprovedBy { get; set; }
            public string ApprovalTimestamp { get; set; }

            public bool? NeedsHumanConfirmation { get; set; }
            public string NeedsHumanConfirmationReason { get; set; }

            public string HumanSummary { get; set; }
            public string CondensedDdrContent { get; set; }
            public string RagIndexCard { get; set; }
            public string ReferentialSummary { get; set; }
            public string[] AgentInstructions { get; set; }

            public bool? DryRun { get; set; }
            public bool? Confirmed { get; set; }
        }

        private sealed class SchemaParseResult
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Type { get; set; }
            public string Status { get; set; }
            public string Summary { get; set; }

            public string Creator { get; set; }
            public string CreationDateRaw { get; set; }
            public string LastUpdatedDateRaw { get; set; }
            public string LastUpdatedBy { get; set; }

            public string Approver { get; set; }
            public string ApprovalTimestampRaw { get; set; }

            public List<DdrChapter> Chapters { get; set; } = new List<DdrChapter>();

            public List<string> Warnings { get; set; } = new List<string>();
            public List<string> Errors { get; set; } = new List<string>();
        }

        private sealed class ImportDdrResult
        {
            public bool Success { get; set; }
            public bool DryRun { get; set; }

            public string Identifier { get; set; }
            public string Title { get; set; }
            public string Type { get; set; }
            public string Status { get; set; }

            public string[] ParseWarnings { get; set; }
            public string[] ParseErrors { get; set; }

            public string SessionId { get; set; }

            public ImportDdrGenerated Generated { get; set; }
        }

        private sealed class ImportDdrGenerated
        {
            public string DdrId { get; set; }
            public string Title { get; set; }
            public string Goal { get; set; }
            public string Type { get; set; }
            public string Status { get; set; }
            public string ApprovedBy { get; set; }
            public string ApprovalTimestamp { get; set; }

            public bool? NeedsHumanConfirmation { get; set; }
            public string NeedsHumanConfirmationReason { get; set; }

            public string HumanSummary { get; set; }
            public string CondensedDdrContent { get; set; }
            public string RagIndexCard { get; set; }
            public string ReferentialSummary { get; set; }
            public string[] AgentInstructions { get; set; }

            public string[] ValidationWarnings { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
            => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);

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

                var parsed = ParseMarkdownSchemaFirstWithCompatibility(args.Markdown);

                if (parsed.Errors.Any())
                {
                    return InvokeResult<string>.FromError("import_ddr failed to parse DDR markdown: " + string.Join(" | ", parsed.Errors));
                }

                if (string.IsNullOrWhiteSpace(parsed.Id))
                {
                    return InvokeResult<string>.FromError("import_ddr could not determine DDR ID from markdown.");
                }

                // Uniqueness check
                var existing = await _ddrManager.GetDdrByTlaIdentiferAsync(parsed.Id, context.Org, context.User, false);
                if (existing != null)
                {
                    return InvokeResult<string>.FromError($"import_ddr - Failed DDR {parsed.Id} already exists as {existing.Name}");
                }

                // Determine final identity/type/status (prefer parsed schema; fall back to args)
                var finalTitle = parsed.Title ?? args.Title;
                var finalType = parsed.Type ?? args.DdrType;
                var finalStatus = parsed.Status ?? args.Status;

                // Validate type semantics (exact match or confirmation required)
                var typeIsAllowed = !string.IsNullOrWhiteSpace(finalType) && AllowedTypes.Contains(finalType, StringComparer.Ordinal);
                var statusIsAllowed = !string.IsNullOrWhiteSpace(finalStatus) && AllowedStatuses.Contains(finalStatus, StringComparer.Ordinal);

                var warnings = new List<string>();
                warnings.AddRange(parsed.Warnings);

                if (string.IsNullOrWhiteSpace(finalTitle))
                    warnings.Add("Title is missing or ambiguous.");

                if (!statusIsAllowed)
                    warnings.Add($"Status '{finalStatus ?? "<null>"}' is missing/invalid (allowed: {string.Join(", ", AllowedStatuses)}).");

                if (!typeIsAllowed)
                    warnings.Add($"DDR Type '{finalType ?? "<null>"}' is missing/invalid (allowed: {string.Join(", ", AllowedTypes)}).");

                // If Approved, approval fields should exist in markdown (compatibility warning if not)
                if (string.Equals(finalStatus, "Approved", StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(parsed.Approver) || string.IsNullOrWhiteSpace(parsed.ApprovalTimestampRaw))
                    {
                        warnings.Add("Status is Approved but '# Approval' is missing Approver and/or Approval Timestamp.");
                    }
                    else if (!IsStrictIsoUtc(parsed.ApprovalTimestampRaw))
                    {
                        warnings.Add("Approval Timestamp is not strict ISO 8601 UTC (yyyy-MM-ddTHH:mm:ss.fffZ).");
                    }
                }

                // Tool-level derived field requirements (these are required for persistence)
                if (string.IsNullOrWhiteSpace(args.HumanSummary))
                    return InvokeResult<string>.FromError("import_ddr did not create 'humanSummary'.");

                if (string.IsNullOrWhiteSpace(args.CondensedDdrContent))
                    return InvokeResult<string>.FromError("import_ddr did not create 'condensedDdrContent'.");

                if (string.IsNullOrWhiteSpace(args.RagIndexCard))
                    return InvokeResult<string>.FromError("import_ddr did not create 'ragIndexCard'.");

                // Type-conditional derived fields
                if (string.Equals(finalType, "Instruction", StringComparison.Ordinal) ||
                    string.Equals(finalType, "Referential", StringComparison.Ordinal))
                {
                    if (args.AgentInstructions == null || args.AgentInstructions.Length == 0)
                        return InvokeResult<string>.FromError("import_ddr did not create 'agentInstructions' for an Instruction or Referential DDR.");

                    if (string.IsNullOrWhiteSpace(args.ReferentialSummary))
                        return InvokeResult<string>.FromError("import_ddr did not create 'referentialSummary' for an Instruction or Referential DDR.");
                }
                else
                {
                    // Generation or Policy / Rules / Governance or unknown
                    if (!string.IsNullOrWhiteSpace(args.ReferentialSummary))
                        return InvokeResult<string>.FromError($"import_ddr must not include 'referentialSummary' for this DDR type ({finalType}).");

                    if (args.AgentInstructions != null && args.AgentInstructions.Length > 0)
                        return InvokeResult<string>.FromError($"import_ddr must not include 'agentInstructions' for this DDR type ({finalType}).");
                }

                // Compute needsHumanConfirmation (compatibility mode)
                var needsHumanConfirmation =
                    args.NeedsHumanConfirmation == true ||
                    warnings.Any() ||
                    string.Equals(finalType, "Policy / Rules / Governance", StringComparison.Ordinal);

                var needsReason = BuildNeedsReason(args.NeedsHumanConfirmationReason, warnings);

                // If needs confirmation and not confirmed, return preview
                var dryRun = args.DryRun.GetValueOrDefault(false);
                if (dryRun || args.Confirmed != true || needsHumanConfirmation)
                {
                    var preview = new ImportDdrResult
                    {
                        Success = true,
                        DryRun = true,
                        Identifier = parsed.Id,
                        Title = finalTitle,
                        Type = finalType,
                        Status = finalStatus,
                        ParseWarnings = warnings.Any() ? warnings.ToArray() : Array.Empty<string>(),
                        ParseErrors = parsed.Errors.Any() ? parsed.Errors.ToArray() : Array.Empty<string>(),
                        SessionId = context?.SessionId,
                        Generated = new ImportDdrGenerated
                        {
                            DdrId = args.DdrId,
                            Title = args.Title,
                            Goal = args.Goal,
                            Type = args.DdrType,
                            Status = args.Status,
                            ApprovedBy = args.ApprovedBy,
                            ApprovalTimestamp = args.ApprovalTimestamp,
                            NeedsHumanConfirmation = needsHumanConfirmation,
                            NeedsHumanConfirmationReason = needsReason,
                            HumanSummary = args.HumanSummary,
                            CondensedDdrContent = args.CondensedDdrContent,
                            RagIndexCard = args.RagIndexCard,
                            ReferentialSummary = args.ReferentialSummary,
                            AgentInstructions = args.AgentInstructions,
                            ValidationWarnings = warnings.ToArray()
                        }
                    };

                    return InvokeResult<string>.Create(JsonConvert.SerializeObject(preview));
                }

                // Confirmed path: persist
                var now = DateTime.UtcNow.ToJSONString();

                if (!TryParseSchemaId(parsed.Id, out var tla, out var idx))
                {
                    return InvokeResult<string>.FromError($"import_ddr parsed DDR ID '{parsed.Id}' is not valid (expected AAA-NNNNNN).");
                }

                var ddr = new DetailedDesignReview
                {
                    DdrIdentifier = parsed.Id,
                    Tla = tla,
                    Index = idx,

                    CreatedBy = context.User,
                    LastUpdatedBy = context.User,
                    OwnerOrganization = context.Org,

                    Key = parsed.Id.ToLowerInvariant().Replace("-", string.Empty),

                    CreationDate = now,
                    LastUpdatedDate = now,

                    Name = finalTitle,
                    Type = finalType,
                    Status = finalStatus,
                    StatusTimestamp = now,

                    Goal = args.Goal,

                    HumanSummary = args.HumanSummary,
                    CondensedDdrContent = args.CondensedDdrContent,
                    RagIndexCard = args.RagIndexCard,
                    ReferentialSummary = args.ReferentialSummary,
                    AgentInstructions = args.AgentInstructions == null ? null : string.Join("\n", args.AgentInstructions),

                    NeedsHumanConfirmation = needsHumanConfirmation,
                    NeedsHumanConfirmationReason = needsReason,

                    FullDDRMarkDown = args.Markdown,
                    Chapters = parsed.Chapters ?? new List<DdrChapter>()
                };

                // Approval persistence: use context.User per your instruction
                if (string.Equals(finalStatus, "Approved", StringComparison.Ordinal))
                {
                    ddr.ApprovedBy = context.User;

                    // Prefer strict schema timestamp from markdown; else accept args raw timestamp
                    var ts = parsed.ApprovalTimestampRaw;
                    if (string.IsNullOrWhiteSpace(ts))
                        ts = args.ApprovalTimestamp;

                    ddr.ApprovedTimestamp = ts;
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
                    Identifier = parsed.Id,
                    Title = finalTitle,
                    Type = finalType,
                    Status = finalStatus,
                    ParseWarnings = warnings.Any() ? warnings.ToArray() : Array.Empty<string>(),
                    ParseErrors = parsed.Errors.Any() ? parsed.Errors.ToArray() : Array.Empty<string>(),
                    SessionId = context?.SessionId,
                    Generated = new ImportDdrGenerated
                    {
                        DdrId = args.DdrId,
                        Title = args.Title,
                        Goal = args.Goal,
                        Type = args.DdrType,
                        Status = args.Status,
                        ApprovedBy = args.ApprovedBy,
                        ApprovalTimestamp = args.ApprovalTimestamp,
                        NeedsHumanConfirmation = needsHumanConfirmation,
                        NeedsHumanConfirmationReason = needsReason,
                        HumanSummary = args.HumanSummary,
                        CondensedDdrContent = args.CondensedDdrContent,
                        RagIndexCard = args.RagIndexCard,
                        ReferentialSummary = args.ReferentialSummary,
                        AgentInstructions = args.AgentInstructions,
                        ValidationWarnings = warnings.ToArray()
                    }
                };

                return InvokeResult<string>.Create(JsonConvert.SerializeObject(result));
            }
            catch (ValidationException vex)
            {
                _logger.AddException("[ImportDdrTool_ExecuteAsync__ValidationException]", vex);
                return InvokeResult<string>.FromError($"import_ddr failed validation problem(s): {string.Join(",", vex.Errors.Select(err => err))}");
            }
            catch (Exception ex)
            {
                _logger.AddException("[ImportDdrTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError($"import_ddr failed to process arguments: {ex.Message}.");
            }
        }

        private static string BuildNeedsReason(string explicitReason, List<string> warnings)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(explicitReason))
                parts.Add(explicitReason.Trim());

            if (warnings != null && warnings.Any())
                parts.Add("Compatibility warnings: " + string.Join(" | ", warnings));

            return parts.Count == 0 ? null : string.Join(" | ", parts);
        }

        private SchemaParseResult ParseMarkdownSchemaFirstWithCompatibility(string markdown)
        {
            var result = new SchemaParseResult();

            // Normalize newlines
            markdown = markdown.Replace("\r\n", "\n");

            // Try schema block split
            var blocks = TrySplitTopLevelBlocks(markdown);
            string metadataBlock = blocks.Metadata;
            string bodyBlock = blocks.Body;
            string approvalBlock = blocks.Approval;

            if (metadataBlock == null || bodyBlock == null || approvalBlock == null)
            {
                result.Warnings.Add("Missing one or more required top-level blocks (# Metadata, # Body, # Approval). Using compatibility parsing.");
            }

            // Parse metadata (strict first)
            var meta = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(metadataBlock))
            {
                meta = ParseFieldLines(metadataBlock, result.Warnings, strict: true);
            }

            // If strict metadata didn't yield ID, try compatibility patterns across whole doc
            var id = GetMeta(meta, "ID");
            if (string.IsNullOrWhiteSpace(id))
            {
                id = MatchFirst(markdown, @"(?mi)^\s*\*\*ID\*\*\s*:\s*(?<v>[A-Za-z]{3}[-_]\d{1,6}|[A-Za-z]{3}-\d{6})\s*$")
                     ?? MatchFirst(markdown, @"(?mi)^\s*ID\s*:\s*(?<v>[A-Za-z]{3}-\d{6})\s*$");
                if (!string.IsNullOrWhiteSpace(id))
                    result.Warnings.Add("ID parsed using legacy/compatibility pattern (not strict schema field line).");
            }

            // Normalize ID (accept AAA-000010; also accept AAA_000010 by converting '_' to '-')
            if (!string.IsNullOrWhiteSpace(id))
                id = id.Trim().Replace("_", "-");

            // Validate ID format
            if (string.IsNullOrWhiteSpace(id))
            {
                result.Errors.Add("Could not find DDR ID.");
                return result;
            }

            if (!Regex.IsMatch(id, @"^[A-Z]{3}-\d{6}$"))
            {
                // Try to coerce if it's AAA-10 -> AAA-000010
                var m = Regex.Match(id, @"^(?<tla>[A-Za-z]{3})-(?<idx>\d{1,6})$");
                if (m.Success)
                {
                    var tla = m.Groups["tla"].Value.ToUpperInvariant();
                    var idx = int.Parse(m.Groups["idx"].Value, CultureInfo.InvariantCulture);
                    id = $"{tla}-{idx:000000}";
                    result.Warnings.Add("ID was not in strict AAA-NNNNNN format; coerced to " + id);
                }
                else
                {
                    result.Errors.Add($"DDR ID '{id}' is not valid. Expected AAA-NNNNNN (e.g., SYS-000010).");
                    return result;
                }
            }

            result.Id = id;

            // Title/Type/Status/Summary from strict metadata if present
            result.Title = GetMeta(meta, "Title") ?? MatchFirst(markdown, @"(?mi)^\s*Title:\s*(?<v>.+?)\s*$");
            result.Type = GetMeta(meta, "Type") ?? MatchFirst(markdown, @"(?mi)^\s*Type:\s*(?<v>.+?)\s*$");
            result.Status = GetMeta(meta, "Status") ?? MatchFirst(markdown, @"(?mi)^\s*Status:\s*(?<v>.+?)\s*$");
            result.Summary = GetMeta(meta, "Summary") ?? MatchFirst(markdown, @"(?mi)^\s*Summary:\s*(?<v>.+?)\s*$");

            // Other schema metadata (optional for import, but warnings if missing)
            result.Creator = GetMeta(meta, "Creator");
            result.CreationDateRaw = GetMeta(meta, "Creation Date");
            result.LastUpdatedDateRaw = GetMeta(meta, "Last Updated Date");
            result.LastUpdatedBy = GetMeta(meta, "Last Updated By");

            // Schema-required metadata warnings (compatibility mode)
            var requiredMeta = new[]
            {
                "ID","Title","Type","Summary","Status","Creator","Creation Date","Last Updated Date","Last Updated By"
            };

            foreach (var req in requiredMeta)
            {
                if (string.IsNullOrWhiteSpace(GetMeta(meta, req)) && req != "ID")
                {
                    // ID handled separately; the rest warn if strict metadata block missing or not strict
                    result.Warnings.Add($"Missing schema metadata field '{req}' in strict '# Metadata' block.");
                }
            }

            if (!string.IsNullOrWhiteSpace(result.CreationDateRaw) && !IsStrictIsoUtc(result.CreationDateRaw))
                result.Warnings.Add("Creation Date is not strict ISO 8601 UTC (yyyy-MM-ddTHH:mm:ss.fffZ).");

            if (!string.IsNullOrWhiteSpace(result.LastUpdatedDateRaw) && !IsStrictIsoUtc(result.LastUpdatedDateRaw))
                result.Warnings.Add("Last Updated Date is not strict ISO 8601 UTC (yyyy-MM-ddTHH:mm:ss.fffZ).");

            // Approval parsing
            if (!string.IsNullOrWhiteSpace(approvalBlock))
            {
                var approvalFields = ParseFieldLines(approvalBlock, result.Warnings, strict: false);
                result.Approver = GetMeta(approvalFields, "Approver");
                result.ApprovalTimestampRaw = GetMeta(approvalFields, "Approval Timestamp");
            }
            else
            {
                // compatibility scan
                result.Approver = MatchFirst(markdown, @"(?mi)^\s*Approver:\s*(?<v>.+?)\s*$")
                                  ?? MatchFirst(markdown, @"(?mi)^\s*Approved By:\s*(?<v>.+?)\s*$");
                result.ApprovalTimestampRaw = MatchFirst(markdown, @"(?mi)^\s*Approval Timestamp:\s*(?<v>.+?)\s*$");
                if (!string.IsNullOrWhiteSpace(result.Approver) || !string.IsNullOrWhiteSpace(result.ApprovalTimestampRaw))
                    result.Warnings.Add("Approval parsed using compatibility scan (missing strict '# Approval' block).");
            }

            // Body -> chapters
            if (!string.IsNullOrWhiteSpace(bodyBlock))
            {
                result.Chapters = ParseChaptersFromBody(bodyBlock, result.Warnings);
            }
            else
            {
                // compatibility: attempt to parse chapters from whole doc
                result.Chapters = ParseChaptersFromBody(markdown, result.Warnings);
                result.Warnings.Add("Chapters parsed from full document (missing strict '# Body' block).");
            }

            return result;
        }

        private static (string Metadata, string Body, string Approval) TrySplitTopLevelBlocks(string markdown)
        {
            // Find exact headings
            var metaIdx = IndexOfHeading(markdown, "# Metadata");
            var bodyIdx = IndexOfHeading(markdown, "# Body");
            var apprIdx = IndexOfHeading(markdown, "# Approval");

            if (metaIdx < 0 || bodyIdx < 0 || apprIdx < 0)
                return (null, null, null);

            if (!(metaIdx <= bodyIdx && bodyIdx <= apprIdx))
                return (null, null, null);

            var metadata = markdown.Substring(metaIdx, bodyIdx - metaIdx);
            var body = markdown.Substring(bodyIdx, apprIdx - bodyIdx);
            var approval = markdown.Substring(apprIdx);

            return (metadata, body, approval);
        }

        private static int IndexOfHeading(string text, string heading)
        {
            // Match heading at start of line
            var m = Regex.Match(text, @"(?m)^\s*" + Regex.Escape(heading) + @"\s*$");
            return m.Success ? m.Index : -1;
        }

        private static Dictionary<string, string> ParseFieldLines(string block, List<string> warnings, bool strict)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            var lines = block.Replace("\r\n", "\n").Split('\n');

            foreach (var raw in lines)
            {
                var line = raw?.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Skip headings
                if (line.StartsWith("#"))
                    continue;

                // Strict schema: "Field Name: value" exactly (no bold markers)
                if (strict && line.Contains("**"))
                {
                    warnings?.Add("Strict metadata contains bold formatting; expected 'Field: value' lines.");
                    continue;
                }

                var m = Regex.Match(line, @"^(?<k>[^:]+):\s*(?<v>.*)$");
                if (!m.Success)
                    continue;

                var key = m.Groups["k"].Value.Trim();
                var val = m.Groups["v"].Value.Trim();

                if (!string.IsNullOrWhiteSpace(key))
                    dict[key] = string.IsNullOrWhiteSpace(val) ? null : val;
            }

            return dict;
        }

        private static string GetMeta(Dictionary<string, string> dict, string key)
        {
            if (dict == null) return null;
            return dict.TryGetValue(key, out var v) ? v : null;
        }

        private static List<DdrChapter> ParseChaptersFromBody(string bodyBlock, List<string> warnings)
        {
            var chapters = new List<DdrChapter>();
            var text = bodyBlock.Replace("\r\n", "\n");

            // Find all "## ..." headings
            var matches = Regex.Matches(text, @"(?m)^##\s+(?<h>.+?)\s*$");
            if (matches.Count == 0)
            {
                warnings?.Add("No chapters found (expected '## <number> - <title>').");
                return chapters;
            }

            for (int i = 0; i < matches.Count; i++)
            {
                var start = matches[i].Index;
                var end = (i + 1 < matches.Count) ? matches[i + 1].Index : text.Length;
                var headingLine = matches[i].Groups["h"].Value.Trim();
                var chunk = text.Substring(start, end - start).Trim();

                // Parse schema chapter heading: "1 - Overview"
                var hm = Regex.Match(headingLine, @"^(?<num>\d+(\.\d+)*)\s+-\s+(?<title>.+)$");
                string title;
                if (hm.Success)
                {
                    title = hm.Groups["title"].Value.Trim();
                }
                else
                {
                    title = headingLine;
                    warnings?.Add($"Chapter heading not in schema form '## <number> - <title>': '{headingLine}'.");
                }

                // Find immediate Summary line after heading
                // Remove first heading line from chunk
                var chunkLines = chunk.Split('\n').ToList();
                if (chunkLines.Count > 0)
                    chunkLines.RemoveAt(0);

                // Skip blank lines
                while (chunkLines.Count > 0 && string.IsNullOrWhiteSpace(chunkLines[0]))
                    chunkLines.RemoveAt(0);

                string summary = null;
                if (chunkLines.Count > 0)
                {
                    var sm = Regex.Match(chunkLines[0].Trim(), @"^Summary:\s*(?<v>.+)$");
                    if (sm.Success)
                    {
                        summary = sm.Groups["v"].Value.Trim();
                        chunkLines.RemoveAt(0);
                    }
                    else
                    {
                        warnings?.Add($"Chapter '{title}' missing immediate 'Summary:' line.");
                        // Compatibility: use first non-empty line as summary if it isn't another heading
                        var first = chunkLines[0].Trim();
                        if (!first.StartsWith("#"))
                        {
                            summary = first.Length > 200 ? first.Substring(0, 200) : first;
                        }
                    }
                }

                // Remaining lines become Details (preserve as markdown-ish text)
                var details = string.Join("\n", chunkLines).Trim();
                if (string.IsNullOrWhiteSpace(details))
                    details = null;

                chapters.Add(new DdrChapter
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = title,
                    Summary = summary,
                    Details = details,
                    Status = null
                });
            }

            return chapters;
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

        private static bool TryParseSchemaId(string identifier, out string tla, out int index)
        {
            tla = null;
            index = 0;
            if (string.IsNullOrWhiteSpace(identifier))
                return false;

            var m = Regex.Match(identifier.Trim(), @"^(?<tla>[A-Z]{3})-(?<idx>\d{6})$");
            if (!m.Success)
                return false;

            tla = m.Groups["tla"].Value.ToUpperInvariant();
            if (!int.TryParse(m.Groups["idx"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
                return false;

            return true;
        }

        private static bool IsStrictIsoUtc(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // Strict: yyyy-MM-ddTHH:mm:ss.fffZ
            return DateTimeOffset.TryParseExact(
                value.Trim(),
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _);
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Imports a DDR from a Markdown document (create-only). Markdown is authoritative. " +
                "Parses SYS-000010 schema blocks with compatibility fallback and supports dry-run/confirmation before persistence.",
                p =>
                {
                    p.String("markdown", "Full authoritative DDR Markdown content to import.", required: true);
                    p.String("source", "Optional source label (filename/path/URL) for traceability.");
                    p.Boolean("dryRun", "If true, do not persist; return parsed identity and generated fields for review.");
                    p.Boolean("confirmed", "If true, indicates the human has confirmed values and the tool may persist.");

                    p.String("ddrId", "DDR identifier extracted from Markdown (e.g., 'SYS-000010'). If unknown/ambiguous, set null and needsHumanConfirmation=true.");
                    p.String("title", "DDR title extracted from Markdown. If unknown/ambiguous, set null and needsHumanConfirmation=true.");
                    p.String("ddrType", "DDR type extracted from Markdown. Must be exactly one of: Instruction, Referential, Generation, Policy / Rules / Governance. If not exact, set needsHumanConfirmation=true.");
                    p.String("goal", "Goal extracted from Markdown; may be synthesized if clear. If unclear, set needsHumanConfirmation=true.");
                    p.String("status", "DDR status extracted from Markdown (Draft/In Review/Approved/Tabled/Cancelled/Superseded). If missing/ambiguous, set null and needsHumanConfirmation=true.");
                    p.String("approvedBy", "Approver name extracted from '# Approval' -> 'Approver:'. If missing/ambiguous, set null and needsHumanConfirmation=true.");
                    p.String("approvalTimestamp", "Approval timestamp extracted from '# Approval' -> 'Approval Timestamp:'. Preserve raw value. If missing/ambiguous, set null and needsHumanConfirmation=true.");

                    p.Boolean("needsHumanConfirmation", "True if identity fields are null/ambiguous, approval is missing/inconsistent for Approved DDRs, schema violations require review, or ddrType is Policy / Rules / Governance.");
                    p.String("needsHumanConfirmationReason", "Reason for needsHumanConfirmation=true.");

                    p.String("humanSummary", "Human-facing summary (1–2 sentences) describing purpose/scope; no procedural steps; no new rules.", required: true);
                    p.String("condensedDdrContent", "Condensed DDR content preserving normative meaning; no new rules/interpretations.", required: true);
                    p.String("ragIndexCard", "RAG routing-only index card (1–2 sentences). Must include DDR ID/Type/Status/approval metadata and purpose; must not contain MUST/MUST NOT/SHOULD/MAY.", required: true);

                    p.String("referentialSummary", "Model generated referential summary, ultra-condensed awareness marker. Always required for ddrTypes 'Instruction' and 'Referential';");
                    p.StringArray("agentInstructions", "Model generated executable authoritative instructions. Always required for ddrTypes 'Instruction' and 'Referential';");
                });
        }
    }
}