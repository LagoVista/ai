using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// TUL-XXX: Workspace TOC Tool (workspace_toc_get).
    ///
    /// This tool definition follows AGN-005 for schema and usage metadata,
    /// but the actual workspace scan and parsing is performed on the client side.
    ///
    /// </summary>
    public class WorkspaceTocGetTool : IAgentTool
    {
        public const string ToolName = "workspace_toc_get";
        private readonly IAdminLogger _logger;

        public bool IsToolFullyExecutedOnServer => false;

        public WorkspaceTocGetTool(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public const string ToolSummary = "get a source-only table-of-contents (TOC) for the user workspace using a client side tool";
        public string Name => ToolName;

        /// <summary>
        /// DTO representing the arguments expected from the LLM.
        /// The server does not execute the scan; the client enforces safety rules.
        /// </summary>
        private sealed class Args
        {
            [JsonProperty("root")]
            public string Root { get; set; } = ".";

            [JsonProperty("useGitignore")]
            public bool? UseGitignore { get; set; }

            [JsonProperty("includeContentHints")]
            public bool? IncludeContentHints { get; set; }

            [JsonProperty("maxBytesPerFile")]
            public int? MaxBytesPerFile { get; set; }

            [JsonProperty("maxFiles")]
            public int? MaxFiles { get; set; }
        }

        /// <summary>
        /// DTO for server-side tool response (safety net).
        /// Normal flows should not call ExecuteAsync for this tool.
        /// </summary>
        private sealed class Result
        {
            public string ToolName { get; set; } = WorkspaceTocGetTool.ToolName;
            public string SessionId { get; set; } = string.Empty;

            public bool IsClientExecutedOnly { get; set; } = true;
            public string Message { get; set; } = string.Empty;

            public Args? Args { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
            => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                Args? args = null;

                if (!string.IsNullOrWhiteSpace(argumentsJson))
                {
                    try
                    {
                        args = JsonConvert.DeserializeObject<Args>(argumentsJson);

                        // Light validation (do not throw per AGN-005)
                        if (args != null)
                        {
                            if (string.IsNullOrWhiteSpace(args.Root))
                                return InvokeResult<string>.FromError("workspace_toc_get requires 'root' to be '.' or a non-empty relative path.");

                            // Basic path safety hinting (client must enforce)
                            if (args.Root.Contains(".."))
                                return InvokeResult<string>.FromError("workspace_toc_get 'root' must not contain '..' path traversal.");

                            if (args.MaxBytesPerFile.HasValue && args.MaxBytesPerFile.Value <= 0)
                                return InvokeResult<string>.FromError("workspace_toc_get 'maxBytesPerFile' must be > 0 when provided.");

                            if (args.MaxFiles.HasValue && args.MaxFiles.Value <= 0)
                                return InvokeResult<string>.FromError("workspace_toc_get 'maxFiles' must be > 0 when provided.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.AddException(this.Tag(), ex);
                        // Do not fail hard on deserialization; continue to safety-net response below.
                    }
                }

                _logger.Trace($"[JSON.WorkspaceTocGet]{argumentsJson}");
                _logger.Trace($"{this.Tag()} workspace_toc_get was executed on the server (client-only tool).");

                var result = new Result
                {
                    SessionId = context?.SessionId ?? string.Empty,
                    Message = "workspace_toc_get is intended for client execution only. If you see this result, the tool pipeline is misconfigured.",
                    Args = args
                };

                return await Task.FromResult(InvokeResult<string>.Create(JsonConvert.SerializeObject(result)));
            }
            catch (Exception ex)
            {
                _logger.AddException("[WorkspaceTocGetTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("workspace_toc_get failed to process arguments on the server. This tool is intended for client execution only.");
            }
        }

        /// <summary>
        /// OpenAI tool/function schema describing workspace_toc_get.
        /// Used by the Reasoner to populate the /responses tools array.
        /// </summary>
        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Return a source-only flat Table of Contents (TOC) for the user's workspace. The scan is performed on the client side; the server does not read the filesystem. Output is a JSON object with a 'files' array of lines: 'path | kind | short description | symbols'. The client may also include an optional 'stats' object (scanned/included/excluded) for diagnostics.",
                p =>
                {
                    p.String("root", "Workspace-relative root folder to scan (default '.'). Must be relative and must not contain '..'.", required: false);
                    p.Boolean("useGitignore", "Optional (default true). When true, the client uses .gitignore patterns to exclude files. The client still applies a source-only allowlist.", required: false);
                    p.Boolean("includeContentHints", "Optional (default true). When true, the client reads up to maxBytesPerFile and extracts symbols (multiple classes/interfaces/enums) and base class/extends when available.", required: false);
                    p.Number("maxBytesPerFile", "Optional (default 65536). Max bytes to read per file for symbol extraction.", required: false);
                    p.Number("maxFiles", "Optional (default 5000). Safety cap on number of files returned.", required: false);
                });
        }

        /// <summary>
        /// LLM-facing usage guidance for workspace_toc_get.
        /// </summary>
        public const string ToolUsageMetadata = @"""
workspace_toc_get — Usage Guide

Primary purpose:
- Retrieve a flat, source-only inventory of files in the user's workspace so you can choose exact file paths to read/edit/create.
- This is an orientation tool (a repo menu), not a source of truth for file contents.

Client-executed only:
- The server does not read the filesystem for this tool.
- The client performs the scan and returns the TOC as JSON.

Output format:
- JSON object with:
- files: array of strings, one per file, formatted as:
    'path | kind | short description | symbols'
- stats (optional): object for diagnostics, such as:
    { scanned: number, included: number, excluded: number }
- 'symbols' is a compact list such as:
  'class Foo : ControllerBase; class Bar; interface IBaz; enum Mode'

Source-only rule:
- The TOC includes only source files (e.g., .cs, .ts, .tsx, .js, .jsx).
- Non-source assets (images, binaries, build outputs) are excluded.

Gitignore behavior:
- When useGitignore=true (default), the client uses .gitignore patterns to exclude files.
- The client still enforces a source-only allowlist even if .gitignore is permissive.

When to use:
- At the start of a coding session or when you need to locate the correct files to modify.
- Before requesting file reads, patches, or creating new files.

How to use (hard rules):
- Use the TOC to choose candidate files by exact path.
- Request at most 1–3 files at a time by exact path using workspace read tools.
- Do not ask for directory uploads or broad file dumps.

Arguments:
- root (string, optional, default '.'): workspace-relative folder to scan.
- useGitignore (bool, optional, default true): apply .gitignore excludes.
- includeContentHints (bool, optional, default true): extract multiple symbols per file (classes/interfaces/enums) and base types.
- maxBytesPerFile (number, optional, default 65536): cap per-file reads for symbol extraction.
- maxFiles (number, optional, default 5000): safety cap.

Error semantics:
- Real filesystem/scan errors (permission, read failures, too large) are produced by the client implementation.
- If this tool returns a server-side payload indicating client-only execution, it usually indicates a misconfiguration of the tool pipeline.
""";
    }
}