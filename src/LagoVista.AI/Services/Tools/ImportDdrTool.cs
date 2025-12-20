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
    /// v1: No chapters. Best-effort parsing. Enforces identifier <-> TLA/IDX consistency.
    /// If TLA-IDX already exists, reject and tell user what exists (TODO: wire DDR lookup).
    /// Approval fields are parsed when possible and should be confirmed by the user before being applied.
    /// </summary>
    public sealed class ImportDdrTool : IAgentTool
    {
        private readonly IAdminLogger _logger;
        private readonly IDdrManager _ddrManager;

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolName = "import_ddr";

        public const string ToolUsageMetadata =
            "Imports a DDR from a Markdown document into the DDR store (create-only). " +
            "Use when the user provides a DDR Markdown file and wants it imported. " +
            "The tool parses identifier/TLA/index/title/status and approval metadata when possible. " +
            "If the TLA-index already exists, the tool must reject and report which DDR currently exists. " +
            "If the parsed identifier does not match the parsed TLA-index, return an error. " +
            "When you import the DDR, you should create a compact version of it to be passed to a LLM to establish context, this should be in the jsonl argument." +
            "Chapters are not imported in the first cut. " +
            "If you can not extract a clear summary section, please create one or two sentances summarizing the DDR content be examining the content in the markdown. " +
            "Any unparseable fields are returned as null/unknown and should be confirmed with the user.";

        public ImportDdrTool(IDdrManager ddrManager, IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ddrManager = ddrManager ?? throw new ArgumentNullException(nameof(ddrManager));
        }

        private sealed class ImportDdrArgs
        {
            public string Markdown { get; set; }
            public string Source { get; set; }

            public string LlmSummary { get; set; }

            public string HumanSummary { get; set; }

            public string RagSummary { get; set; }
            /// <summary>
            /// If true, do not write anything; just parse and return what would be imported.
            /// This supports "confirm with user" flows for approvals and metadata.
            /// </summary>
            public bool? DryRun { get; set; }

            /// <summary>
            /// Optional flag to indicate the user has confirmed parsed values and wants them applied.
            /// If not set/false, you can choose to treat as parse-only depending on your UX.
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

            public string Identifier { get; set; }
            public string Tla { get; set; }
            public int? Index { get; set; }
            public string Title { get; set; }
            public string Status { get; set; }

            public ImportDdrParsed Parsed { get; set; }

            public string ConversationId { get; set; }
            public string SessionId { get; set; }
        }

        public async Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return  InvokeResult<string>.FromError("import_ddr requires a non-empty arguments object.");
            }

            try
            {
                var args = JsonConvert.DeserializeObject<ImportDdrArgs>(argumentsJson) ?? new ImportDdrArgs();

                if (string.IsNullOrWhiteSpace(args.Markdown))
                {
                    return  InvokeResult<string>.FromError("import_ddr requires 'markdown' containing the DDR content.");
                }

                var parsed = ParseMarkdown(args.Markdown);

                // Hard validation: we need TLA + Index + Identifier to enforce mismatch rules.
                if (string.IsNullOrWhiteSpace(parsed.Identifier))
                {
                    return InvokeResult<string>.FromError("import_ddr could not parse 'identifier' from the Markdown.");
                }

                if (string.IsNullOrWhiteSpace(parsed.Tla) || !parsed.Index.HasValue)
                {
                    return InvokeResult<string>.FromError("import_ddr could not parse both 'tla' and 'index' from the Markdown.");
                }

                if (string.IsNullOrWhiteSpace(args.Jsonl))
                {
                    return InvokeResult<string>.FromError("import_ddr did not summarize the markdown as JSONL for LLM.");
                }

                if (string.IsNullOrEmpty(parsed.Summary) && string.IsNullOrEmpty(args.HumanSummary))
                {
                    return InvokeResult<string>.FromError("import_ddr did not create a summary a human, this should either be extracted or generated.");
                }

                if (string.IsNullOrEmpty(args.RagSummary))
                {
                    return InvokeResult<string>.FromError("import_ddr did not create a summary for indexing, this should either be extracted or generated.");
                }

                // Hard rule: identifier must match TLA/IDX (this should never happen)
                if (!TryParseIdentifier(parsed.Identifier, out var identTla, out var identIdx))
                {
                    return InvokeResult<string>.FromError($"import_ddr could not parse tla/index from identifier '{parsed.Identifier}'.");
                }

                if (!string.Equals(identTla, parsed.Tla, StringComparison.OrdinalIgnoreCase) ||
                    identIdx != parsed.Index.Value)
                {
                    return InvokeResult<string>.FromError(
                            $"import_ddr identifier mismatch: Markdown indicates {parsed.Tla}-{parsed.Index:000} but identifier is '{parsed.Identifier}' (parsed as {identTla}-{identIdx:000}).");
                }

                var dryRun = args.DryRun.GetValueOrDefault(false);

                // TODO: existence check by TLA+IDX in your DDR store
                // If exists -> reject and tell user which one currently exists.
                //
                // Example pseudo:
                // var existing = await _ddrRepo.FindByTlaIndexAsync(parsed.Tla, parsed.Index.Value);
                // if(existing != null) return FromError($"DDR {parsed.Tla}-{parsed.Index:000} already exists as {existing.Identifier} '{existing.Title}'");
                //
                // For now, leave as a stub with a clear TODO.
                //
                // IMPORTANT: In your wired implementation, do this check BEFORE writing.

                if (dryRun || args.Confirmed != true)
                {
                    // Parse-only response to support "confirm with user" (especially approvals).
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
                        ConversationId = context?.Request?.ConversationId,
                        SessionId = context?.SessionId
                    };

                    var existingDdr = await _ddrManager.GetDdrByTlaIdentiferAsync(parsed.Identifier, context.Org, context.User, false);
                    if(existingDdr != null)
                        return InvokeResult<string>.FromError($"import_ddr - Failed DDR {parsed.Identifier} already exists as {existingDdr.Name}");
                   
                    return InvokeResult<string>.Create(JsonConvert.SerializeObject(preview));
                }

                // TODO: Create DDR in storage (create-only), set metadata (title/summary/status free-form),
                // and apply approval values (as confirmed) to the stored DDR.
                //
                // For now, return a stub "success" to show the intended contract.
                var result = new ImportDdrResult
                {
                    Success = true,
                    DryRun = false,
                    Identifier = parsed.Identifier,
                    Tla = parsed.Tla,
                    Index = parsed.Index,
                    Title = parsed.Title,
                    Status = parsed.Status,
                    Parsed = parsed,
                    ConversationId = context?.Request?.ConversationId,
                    SessionId = context?.SessionId
                };

                var timeStamp = DateTime.UtcNow.ToJSONString();

                var ddr = new DetailedDesignReview
                {
                    DdrIdentifier = parsed.Identifier,
                    CreatedBy = context.User,
                    Key = parsed.Identifier.ToLower().Replace("-", String.Empty),
                    LastUpdatedBy = context.User,
                    CreationDate = timeStamp,   
                    LastUpdatedDate = timeStamp,
                    OwnerOrganization = context.Org,
                    Tla = parsed.Tla,
                    Index = parsed.Index.Value,
                    Name = parsed.Title,
                    LlmSummary = args.Jsonl,
                    Summary = parsed.Summary ?? args.HumanSummary,
                    RagSummary = args.RagSummary,
                    Status = parsed.Status,
                    StatusTimestamp  = timeStamp,
                    FullDDRMarkDown = args.Markdown                    
                };

                if(parsed.Approval != null && parsed.Approval.ApprovalTimestamp.HasValue)
                {
                    ddr.ApprovedBy = context.User;
                    ddr.ApprovedTimestamp = parsed.Approval.ApprovalTimestampRaw;
                }

                var addResult = await _ddrManager.AddDdrAsync(ddr, context.Org, context.User);
                if(!addResult.Successful)
                    return InvokeResult<string>.FromError($"import_ddr failed to create DDR: {addResult.ErrorMessage}"); 
                return InvokeResult<string>.Create(JsonConvert.SerializeObject(result));
            }
            catch(ValidationException vex)
            {
                _logger.AddException("[ImportDdrTool_ExecuteAsync__ValidationException]", vex);
                return InvokeResult<string>.FromError($"import_ddr failed validation problem(s): {String.Join(",", vex.Errors.Select(err=>err))}");
            }
            catch (Exception ex)
            {
                _logger.AddException("[ImportDdrTool_ExecuteAsync__Exception]", ex);

                return InvokeResult<string>.FromError($"import_ddr failed to process arguments: {ex.Message}.");
            }
        }

        private static ImportDdrParsed ParseMarkdown(string markdown)
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
            // If you standardize a Summary field later, add it here.
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
            if (string.IsNullOrEmpty(text)) return null;

            var m = Regex.Match(text, pattern);
            if (!m.Success) return null;

            var v = m.Groups["v"]?.Value;
            return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }

        private static string TryParseTitleFromH1(string markdown)
        {
            // Example: "# TUL-011 — Agent Tool Contract"
            var h1 = MatchFirst(markdown, @"(?mi)^\#\s*(?<v>.+?)\s*$");
            if (string.IsNullOrWhiteSpace(h1)) return null;

            // If H1 contains " — " or " - ", attempt to strip leading identifier
            var parts = h1
                .Split(new[] { "—", "-" }, 2, StringSplitOptions.None)
                .Select(p => p.Trim())
                .ToArray();
            
            if (parts.Length == 2 && parts[0].Length <= 16)
            {
                return parts[1].Trim();
            }

            return h1.Trim();
        }

        private static bool TryParseIdentifier(string identifier, out string tla, out int index)
        {
            tla = null;
            index = 0;

            if (string.IsNullOrWhiteSpace(identifier)) return false;

            // Accept formats like TUL-011, TUL_011, TUL011 (last one optional if you want).
            var m = Regex.Match(identifier.Trim(), @"^(?<tla>[A-Za-z]{2,10})[-_](?<idx>\d{1,6})$");
            if (!m.Success) return false;

            tla = m.Groups["tla"].Value.ToUpperInvariant();
            if (!int.TryParse(m.Groups["idx"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            {
                return false;
            }

            return true;
        }

        private static DateTimeOffset? TryParseDateTimeOffsetLoose(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

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

        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Imports a DDR from a Markdown document (create-only). Parses identifier/TLA/index/title/status and approval metadata when possible; returns parsed values for confirmation and can apply on confirmation.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        markdown = new
                        {
                            type = "string",
                            description = "Full DDR Markdown content to import."
                        },
                        jsonl = new
                        {
                            type = "string",
                            description = "Full DDR Markdown content summaried as JSONL to be consumed by an LLM."
                        },
                        humanSummary = new
                        {
                            type = "string",
                            description = "one or two sentances that summarize the DDR content for human consumption."
                        },
                        ragSummary = new 
                        {
                            type = "string",
                            description = "one or two sentances that summarize the DDR content that will be used to index the DDR for RAG."
                        },
                        source = new
                        {
                            type = "string",
                            description = "Optional source label (e.g., filename/path/URL) to help with traceability."
                        },
                        dryRun = new
                        {
                            type = "boolean",
                            description = "If true, do not write anything; just parse and return extracted fields for confirmation."
                        },
                        confirmed = new
                        {
                            type = "boolean",
                            description = "If true, indicates the user has confirmed parsed values and the tool may proceed to write/apply them."
                        }
                    },
                    required = new[] { "markdown" }
                }
            };

            return schema;
        }
    }
}