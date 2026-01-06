using System;
using System.Collections.Generic;
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
    /// TUL-004: Create File Tool (workspace_create_file).
    ///
    /// This tool definition follows AGN-005 for schema and usage metadata,
    /// but the actual filesystem operation is performed on the client side.
    ///
    /// Server behavior:
    /// - Exposes schema and ToolUsageMetadata to the LLM.
    /// - Should NOT call ExecuteAsync() in normal operation.
    /// - If ExecuteAsync() is called, it returns a structured error payload
    ///   indicating that this tool is client-executed only.
    /// </summary>
    public class WorkspaceCreateFileTool : IAgentTool
    {
        public const string ToolName = "workspace_create_file";
        private readonly IAdminLogger _logger;
        public bool IsToolFullyExecutedOnServer => false;

        public WorkspaceCreateFileTool(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public const string ToolSummary = "create a file on the user machine with a client side tool";
        public string Name => ToolName;

        /// <summary>
        /// DTO representing the arguments expected from the LLM.
        ///
        /// NOTE: The server does not act on these directly for TUL-004;
        /// the client is responsible for enforcing all path and content rules.
        /// </summary>
        private sealed class Args
        {
            [JsonProperty("path")]
            public string Path { get; set; } = string.Empty;

            [JsonProperty("content")]
            public string Content { get; set; } = string.Empty;

            [JsonProperty("overwrite")]
            public bool? Overwrite { get; set; }
        }

        /// <summary>
        /// DTO for the server-side tool response. This is primarily a safety
        /// net; normal flows should not call ExecuteAsync for this tool.
        /// </summary>
        private sealed class Result
        {
            public string ToolName { get; set; } = WorkspaceCreateFileTool.ToolName;
            public string SessionId { get; set; } = string.Empty;
            /// <summary>
            /// Indicates that this tool is intended for client execution only.
            /// If this object is observed in a real tool result, it typically
            /// indicates a misconfiguration of the tool pipeline.
            /// </summary>
            public bool IsClientExecutedOnly { get; set; } = true;
            public string Message { get; set; } = string.Empty;
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        /// <inheritdoc/>
        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // We do not throw on deserialization errors per AGN-005.
                // We also do not act on the arguments for this tool; the
                // filesystem operation is always performed on the client.
                Args args = null;
                if (!string.IsNullOrWhiteSpace(argumentsJson))
                {
                    try
                    {
                        args = JsonConvert.DeserializeObject<Args>(argumentsJson);
                        if( String.IsNullOrEmpty(args.Path)) return InvokeResult<string>.FromError("workspace_create_file requires a non-empty 'path' argument.");
                        if (String.IsNullOrEmpty(args.Content)) return InvokeResult<string>.FromError("workspace_create_file requires a non-empty 'content' argument.");
                    }
                    catch (Exception ex)
                    {
                        _logger.AddException(this.Tag(), ex);
                    }
                }

                _logger.Trace($"[JSON.FileCreate]{argumentsJson}");
                _logger.Trace($"{this.Tag()} workspace_create_file was executed on the server", args.Path.ToKVP("path"), args.Content.Length.ToString().ToKVP("Length"));
                return await Task.FromResult(InvokeResult<string>.Create(argumentsJson));
            }
            catch (Exception ex)
            {
                _logger.AddException("[WorkspaceCreateFileTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("workspace_create_file failed to process arguments on the server. This tool is intended for client execution only.");
            }
        }

        /// <summary>
        /// OpenAI tool/function schema describing workspace_create_file.
        /// This is used by the Reasoner to populate the /responses tools array.
        /// </summary>
        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Create a new file in the user workspace or fully replace an existing file when explicitly allowed. This tool is client-executed only; the server does not perform filesystem I/O.", p =>
            {
                p.String("path", "Workspace-relative file path where the new file should be created. Must not be absolute or escape the workspace root (for example: src/Services/AgentOrchestrator.cs).", required: true);
                p.String("content", "Full text content of the file. The client should write this as UTF-8 without BOM, normalizing line endings to \n.", required: true);
                p.Boolean("overwrite", "Optional. When true, the client may fully replace an existing file at this path. When false or omitted, the client must fail if the file already exists.");
            });
        }

        /// <summary>
        /// LLM-facing usage guidance for workspace_create_file.
        /// This text is surfaced by the Reasoner in the system prompt
        /// alongside the schema.
        /// </summary>
        public const string ToolUsageMetadata = @"""
workspace_create_file — Usage Guide

Primary purpose:
- Request creation of a new text file in the user workspace, or a full overwrite of an existing file when explicitly allowed.
- The actual filesystem operation is performed on the client side, not on the server.

When to use:
- Creating new source files (implementations, services, models, etc.).
- Creating new test files for existing or new code.
- Creating configuration, schema, or documentation files.
- Fully replacing an existing file when you know the complete new content and want an atomic replacement.

When NOT to use:
- Editing or partially modifying an existing file; use patch/edit tools instead.
- Appending content to a file.
- Writing files outside the workspace root (paths that are absolute or escape the root will fail on the client).
- Binary writes (this tool is designed for text content).

Arguments:
- path (string, required):
  - Workspace-relative path to the file.
  - Prefer existing directories from the workspace snapshot and project conventions (e.g., src/ProjectName/..., tests/ProjectName.Tests/..., apps/design-playground/src/app/views/..., libs/primitives/src/lib/...).
- content (string, required):
  - Full text content of the file.
  - The client will write this as UTF-8 without BOM and normalize line endings to \n.
- overwrite (bool, optional, default=false):
  - If true, the client may fully replace an existing file at the given path.
  - If false, the client must fail if the file already exists.

Workspace awareness:
- You will typically receive a snapshot of the workspace directory tree in the boot/system prompt.
- Prefer placing new files into existing directories that match the workspace snapshot and naming conventions.
- Keep implementations and their tests in corresponding src/tests projects when possible.

Ask vs act rule:
- If you can clearly determine the correct directory and file path from the workspace snapshot, project conventions, and conversation context, you may call workspace_create_file directly.
- If there are multiple plausible locations and it is unclear which is correct, ask the user a brief clarifying question before calling the tool.

Important behaviors:
- Always send the complete final content of the file; this tool does not support incremental edits.
- Do not assume that overwrite is allowed; only set overwrite=true when replacement is explicitly intended.
- The client will enforce path safety, size limits, and error codes as described in DDR TUL-004.

Error semantics:
- The server does not perform the filesystem operation for this tool. Any real filesystem errors (such as FileExists, InvalidPath, WriteFailed, TooLarge) are produced by the client implementation and not by this server-side stub.
""";
    }
}