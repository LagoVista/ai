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
    /// Tool: workspace.read_file
    /// Read-only retrieval of a source document using a canonical DocPath
    /// from a RAG snippet header.
    /// Implements the contract defined in TUL-001 and AGN-005.
    /// </summary>
    public class ReadFileTool : IAgentTool
    {
        public const string ToolName = "workspace_read_file";

        private readonly IAdminLogger _logger;
        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolSummary = "used to load a file from a cloud repository.";

        public ReadFileTool(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public const string ToolUsageMetadata = @"
workspace.read_file — Usage Guide

Primary purpose:
- Retrieve the full text of a source document from the Aptix cloud source store.
- Use the canonical DocPath from a RAG snippet header when you need the entire file, not just the snippet.
- This tool is read-only: it never writes or mutates any content and never touches the user’s local filesystem.

When to use:
- You have seen a RAG snippet for a file and need the full document to understand context.
- You need to inspect surrounding code (e.g., other methods, usings, class definitions) that were not included in the snippet.
- You want to verify signatures, imports, or patterns across the entire file.
- You need to reason about the structure or size of the file (sizeBytes, truncation status).

When NOT to use:
- You only need the snippet that is already in context.
- You want to change or rewrite a file; use edit/patch tools instead.
- You are trying to read from the user’s local filesystem (this tool only talks to the Aptix cloud source store).
- The file is clearly an artifact or binary asset that is not stored in the source store.

Arguments:
- path (string, required):
  - The canonical DocPath for the document, taken directly from the RAG snippet header (for example: src/Billing/BillingService.cs).
  - Always pass the DocPath exactly as given; do not modify, normalize, or guess new paths.
- maxBytes (integer, optional):
  - Maximum number of bytes to return from the file.
  - If omitted, the tool returns the entire file content.
  - If specified and the file is larger, the client will truncate and set isTruncated = true.

Execution rules and behavior:
- Before reading from cloud storage, the tool checks the session’s ActiveFiles collection:
  - If the file is already present in ActiveFiles:
    - The tool returns an ALREADY_IN_CONTEXT error payload and does NOT re-fetch the file.
    - In this case, you should reuse the existing content rather than calling workspace.read_file again.
  - If the file is not in ActiveFiles:
    - The tool queries the Aptix cloud source store using the supplied path as-is.
    - On success, it returns success = true with a file payload (path, sizeBytes, content, isTruncated).
    - If the file is not found, it returns NOT_FOUND and you should ask the user to provide or upload the file if it is still needed.

Error semantics:
- ALREADY_IN_CONTEXT:
  - The file is already in ActiveFiles for this session.
  - Do not call workspace.read_file repeatedly for the same DocPath; reuse the existing content.
- NOT_FOUND:
  - The file is not present in ActiveFiles or the cloud source store.
  - Ask the user for guidance (for example, to upload the file or correct the path).
- INVALID_ARGUMENT:
  - The path argument is missing or invalid (for example, empty or whitespace).
  - You should fix the argument and try again with a valid DocPath.
- INTERNAL_ERROR:
  - An unexpected server-side error occurred while serving the request.
  - You may try again or ask the user for help if the problem persists.
- CANCELLED (or equivalent cancellation state):
  - The tool run was cancelled before completion (for example, due to a cancelled request).
";



        public string Name => ToolName;

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);

        public async Task<InvokeResult<string>> ExecuteAsync(
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
                _logger.AddException("[WorkspaceReadFileTool_DeserializeArgs]", ex);

                var errorPayload = WorkspaceReadFileToolResult.CreateError(
                    context,
                    "INVALID_ARGUMENT",
                    "Unable to deserialize arguments for workspace.read_file.");

                invokeResult.Result = JsonConvert.SerializeObject(errorPayload);
                invokeResult.AddUserError("Invalid arguments for workspace.read_file.");
                return invokeResult;
            }

            if (String.IsNullOrWhiteSpace(args.Path))
            {
                var errorPayload = WorkspaceReadFileToolResult.CreateError(
                    context,
                    "INVALID_ARGUMENT",
                    "The 'path' argument is required and must be a non-empty DocPath string.");

                invokeResult.Result = JsonConvert.SerializeObject(errorPayload);
                invokeResult.AddUserError("The 'path' argument is required for workspace.read_file.");
                return invokeResult;
            }

            try
            {
                // TODO: Implement real logic:
                // 1. Check context.ActiveFiles (or equivalent) for args.Path
                // 2. If present -> return ALREADY_IN_CONTEXT
                // 3. Otherwise query cloud source store for full document
                // 4. Return success or NOT_FOUND

                var notImplemented = WorkspaceReadFileToolResult.CreateError(
                    context,
                    "NOT_IMPLEMENTED",
                    "workspace.read_file is scaffolded but not yet wired to the source store and ActiveFiles.");

                invokeResult.Result = JsonConvert.SerializeObject(notImplemented);
                invokeResult.AddSystemError("workspace.read_file is not implemented.");
                await Task.CompletedTask;
                return invokeResult;
            }
            catch (OperationCanceledException)
            {
                var cancelled = new WorkspaceReadFileToolResult
                {
                    Success = false,
                    ErrorCode = "CANCELLED",
                    Errors = new List<string> { "workspace.read_file execution was cancelled." },
                    SessionId = context?.SessionId,
                };

                invokeResult.Result = JsonConvert.SerializeObject(cancelled);
                return invokeResult;
            }
            catch (Exception ex)
            {
                _logger.AddException("[WorkspaceReadFileTool_ExecuteAsync_Exception]", ex);

                var errorPayload = WorkspaceReadFileToolResult.CreateError(
                    context,
                    "INTERNAL_ERROR",
                    "Unexpected error while executing workspace.read_file.");

                invokeResult.Result = JsonConvert.SerializeObject(errorPayload);
                invokeResult.AddSystemError("Unexpected error while executing workspace.read_file.");
                return invokeResult;
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(PingPongTool.ToolName, "Read a text source document from the Aptix cloud source store using a canonical DocPath string.", p =>
            {
                p.String("path", "Canonical DocPath string for the source document (taken directly from the RAG snippet header).");
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
