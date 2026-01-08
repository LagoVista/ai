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
    public class WorkspaceReadServerFileTool : IAgentTool
    {
        public const string ToolName = "workspace_read_server_file";

        private readonly IAdminLogger _logger;
        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolSummary = "used to load a file from a cloud repository.";

        public WorkspaceReadServerFileTool(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public const string ToolUsageMetadata = @"
workspace_read_server_file — Cloud Source Read (Server Tool)

Purpose:
- Read the full text of a source file from the Aptix cloud source store.
- Use when a RAG snippet provides a DocPath and the full file is required.

Use when:
- You need more context than the snippet provides (surrounding code, imports, structure).
- You need to inspect the complete file contents or size/truncation details.

Do NOT use when:
- The snippet already provides sufficient context.
- You intend to modify the file (use edit/patch tools).
- You want to read from the user’s local filesystem.

Arguments:
- path (string, required): canonical DocPath from the RAG snippet (e.g., src/Billing/BillingService.cs).
- maxBytes (int, optional): limit returned content; response sets isTruncated = true if clipped.

Behavior:
- If the file is already in ActiveFiles, returns ALREADY_IN_CONTEXT (do not re-fetch).
- Otherwise reads from the Aptix cloud source store and returns the file payload.
- Returns NOT_FOUND if the file does not exist.

Errors:
- ALREADY_IN_CONTEXT
- NOT_FOUND
- INVALID_ARGUMENT
- INTERNAL_ERROR
- CANCELLED

";



        public string Name => ToolName;

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
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
                return Task.FromResult( invokeResult);
            }

            if (String.IsNullOrWhiteSpace(args.Path))
            {
                var errorPayload = WorkspaceReadFileToolResult.CreateError(
                    context,
                    "INVALID_ARGUMENT",
                    "The 'path' argument is required and must be a non-empty DocPath string.");

                invokeResult.Result = JsonConvert.SerializeObject(errorPayload);
                invokeResult.AddUserError("The 'path' argument is required for workspace.read_file.");
                return Task.FromResult(invokeResult);
            }

            return Task.FromResult(InvokeResult<string>.FromError("SERVER SIDE FILE NOT IMPLEMENTED"));
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
