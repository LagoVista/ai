using System;
using System.Collections.Generic;
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
    /// Tool: workspace_read_file
    /// Client-executed, read-only retrieval of a local workspace file using a canonical DocPath.
    ///
    /// Server responsibility:
    /// - Validate/normalize arguments ONLY (no IO).
    /// - Return a payload that instructs the client runtime to execute the local file read.
    ///
    /// Client responsibility (e.g., VS Code extension):
    /// - Resolve DocPath against the active workspace root.
    /// - Read the file content and return it to the agent runtime.
    /// </summary>
    public class WorkspaceReadClientFileTool : IAgentTool
    {
        public const string ToolName = "workspace_read_client_file";

        private readonly IAdminLogger _logger;

        // IMPORTANT: This is a client-side tool call. Server does validation only.
        public bool IsToolFullyExecutedOnServer => false;

        public const string ToolSummary = "Read a text file from the user's local workspace (client-executed).";

        public WorkspaceReadClientFileTool(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public const string ToolUsageMetadata = @"
workspace_read_client_file — Client Tool (Local Workspace)

Purpose:
- Read the full text of a workspace file from the user’s local filesystem.
- Client-executed (e.g., VS Code / agent client). Server does NOT read files.

When to use:
- You have a DocPath (often from a RAG snippet header) and need the entire file content.

Arguments:
- path (string, required): workspace-relative DocPath (e.g., src/Foo/Bar.cs). No guessing.
- maxBytes (int, optional): limit bytes returned; client truncates and sets isTruncated = true.

Returns: 
- Success/error + file payload including sha256.

Rules:
- Read-only: never writes/patches.
- Must stay within the workspace root (reject absolute paths and '..').

Errors:
- INVALID_ARGUMENT: missing/unsafe path or invalid maxBytes
- NOT_FOUND: file doesn’t exist
- PERMISSION_DENIED: cannot read
- BINARY_FILE / INVALID_ENCODING: not safely readable as text
- INTERNAL_ERROR / CANCELLED
";


        public string Name => ToolName;

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
            => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);

        /// <summary>
        /// Server-side portion: validate arguments and return a payload for the client to execute.
        /// NOTE: No filesystem IO here.
        /// </summary>
        public Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var invokeResult = new InvokeResult<string>();

            WorkspaceReadFileArgs args;

            try
            {
                args = JsonConvert.DeserializeObject<WorkspaceReadFileArgs>(argumentsJson ?? "{}")
                       ?? new WorkspaceReadFileArgs();
            }
            catch (Exception ex)
            {
                _logger.AddException("[WorkspaceReadClientFileTool_DeserializeArgs]", ex);

                var errorPayload = WorkspaceReadFileToolResult.CreateError(
                    context,
                    "INVALID_ARGUMENT",
                    "Unable to deserialize arguments for workspace_read_file.");

                invokeResult.Result = JsonConvert.SerializeObject(errorPayload);
                invokeResult.AddUserError("Invalid arguments for workspace_read_file.");
                return Task.FromResult(invokeResult);
            }

            if (String.IsNullOrWhiteSpace(args.Path))
            {
                var errorPayload = WorkspaceReadFileToolResult.CreateError(
                    context,
                    "INVALID_ARGUMENT",
                    "The 'path' argument is required and must be a non-empty DocPath string.");

                invokeResult.Result = JsonConvert.SerializeObject(errorPayload);
                invokeResult.AddUserError("The 'path' argument is required for workspace_read_file.");
                return Task.FromResult(invokeResult);
            }

            // Server-side guardrail: basic traversal / absolute-path rejection (client should ALSO enforce).
            // Keep this conservative: fail fast if it looks risky.
            var p = args.Path.Trim();
            if (p.StartsWith("/") || p.StartsWith("\\") || p.Contains(".."))
            {
                var errorPayload = WorkspaceReadFileToolResult.CreateError(
                    context,
                    "INVALID_ARGUMENT",
                    "The 'path' must be workspace-relative and must not include absolute prefixes or path traversal ('..').");

                invokeResult.Result = JsonConvert.SerializeObject(errorPayload);
                invokeResult.AddUserError("Invalid 'path' for workspace_read_file.");
                return Task.FromResult(invokeResult);
            }

            if (args.MaxBytes.HasValue && args.MaxBytes.Value <= 0)
            {
                var errorPayload = WorkspaceReadFileToolResult.CreateError(
                    context,
                    "INVALID_ARGUMENT",
                    "If provided, 'maxBytes' must be a positive integer.");

                invokeResult.Result = JsonConvert.SerializeObject(errorPayload);
                invokeResult.AddUserError("Invalid 'maxBytes' for workspace_read_file.");
                return Task.FromResult(invokeResult);
            }

            // Return validated args for the client runtime to execute.
            // (Your tool runtime should treat this as a client tool call payload.)
            return Task.FromResult(InvokeResult<string>.Create(JsonConvert.SerializeObject(args)));
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Read a text file from the user's local workspace using a canonical DocPath string (client-executed).",
                p =>
                {
                    p.String("path", "Workspace-relative DocPath for the file (often taken directly from a RAG snippet header).");
                    p.Integer("maxBytes", "Optional maximum number of bytes to return. If omitted, the entire file is returned.");
                });
        }

        private sealed class WorkspaceReadFileArgs
        {
            [JsonProperty("path")]
            public string Path { get; set; }

            [JsonProperty("maxBytes")]
            public int? MaxBytes { get; set; }
        }

        private sealed class WorkspaceReadFileToolResult
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("errorCode")]
            public string ErrorCode { get; set; }

            [JsonProperty("errors")]
            public List<string> Errors { get; set; }

            [JsonProperty("file")]
            public WorkspaceReadFileFilePayload File { get; set; }

            [JsonProperty("sessionId")]
            public string SessionId { get; set; }

            public static WorkspaceReadFileToolResult CreateError(
                AgentToolExecutionContext context,
                string errorCode,
                string message)
            {
                return new WorkspaceReadFileToolResult
                {
                    Success = false,
                    ErrorCode = errorCode,
                    Errors = new List<string> { message },
                    SessionId = context?.SessionId,
                };
            }
        }

        private sealed class WorkspaceReadFileFilePayload
        {
            [JsonProperty("path")]
            public string Path { get; set; }

            [JsonProperty("sizeBytes")]
            public long SizeBytes { get; set; }

            [JsonProperty("content")]
            public string Content { get; set; }

            [JsonProperty("isTruncated")]
            public bool IsTruncated { get; set; }
        }
    }
}
