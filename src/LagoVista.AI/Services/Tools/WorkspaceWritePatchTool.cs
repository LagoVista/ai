using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Thin IAgentTool wrapper for TUL-002.
    /// All heavy lifting is delegated to WorkspaceWritePatchOrchestrator,
    /// which can be tested independently.
    /// </summary>
    public sealed class WorkspaceWritePatchTool : IAgentTool
    {
        public const string ToolName = "workspace_write_patch";

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => false;

        public const string ToolSummary = "create a set of patches to be applied on the humans machine with a client tool";

        private readonly IWorkspaceWritePatchOrchestrator _orchestrator;

        public WorkspaceWritePatchTool(IWorkspaceWritePatchOrchestrator orchestrator)
        {
            _orchestrator = orchestrator ?? throw new System.ArgumentNullException(nameof(orchestrator));
        }

        public static object GetSchema()
        {
            // Schema matches TUL-002 DDR.
            return new
            {
                type = "function",
                name = ToolName,
                description = "Capture a multi-file, line-based patch batch for one or more workspace files, including SHA256 guards and change descriptions. The client will later apply these patches to local files.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        batchLabel = new
                        {
                            type = "string",
                            description = "Optional human-readable label for this patch batch (for example: Add tests for AgentOrchestrator)."
                        },
                        batchKey = new
                        {
                            type = "string",
                            description = "Optional LLM-chosen stable key for this batch so the LLM can reference it later (for example: agent-orchestrator-tests-v1)."
                        },
                        files = new
                        {
                            type = "array",
                            description = "One or more file patch descriptors.",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    docPath = new
                                    {
                                        type = "string",
                                        description = "Canonical path identifying the file (always lower-case; matches DocPath and Active File List semantics)."
                                    },
                                    fileKey = new
                                    {
                                        type = "string",
                                        description = "Optional LLM-chosen stable key for this file within the batch (for example: orchestrator-tests)."
                                    },
                                    fileLabel = new
                                    {
                                        type = "string",
                                        description = "Optional human-readable label for this file patch (for example: AgentOrchestratorTests.cs)."
                                    },
                                    originalSha256 = new
                                    {
                                        type = "string",
                                        description = "SHA256 of the file content the LLM used when preparing this patch. Used by the client to detect drift before applying.",
                                        minLength = 64,
                                        maxLength = 64
                                    },
                                    changes = new
                                    {
                                        type = "array",
                                        description = "Ordered list of line-based changes to apply to this file.",
                                        items = new
                                        {
                                            type = "object",
                                            properties = new
                                            {
                                                changeKey = new
                                                {
                                                    type = "string",
                                                    description = "Optional LLM-chosen stable key for this change, used to ask follow-up questions or request modifications."
                                                },
                                                operation = new
                                                {
                                                    type = "string",
                                                    description = "Type of line-based operation.",
                                                    // insert, replace, delete
                                                    enumValues = new[] { "insert", "replace", "delete" }
                                                },
                                                description = new
                                                {
                                                    type = "string",
                                                    description = "Short explanation of why this change is being made. Keep it concise but meaningful."
                                                },
                                                afterLine = new
                                                {
                                                    type = "integer",
                                                    description = "For insert: 1-based line number AFTER which newLines should be inserted. Use 0 to insert at top of file."
                                                },
                                                startLine = new
                                                {
                                                    type = "integer",
                                                    description = "For replace or delete: 1-based starting line number (inclusive) of the block being modified."
                                                },
                                                endLine = new
                                                {
                                                    type = "integer",
                                                    description = "For replace or delete: 1-based ending line number (inclusive) of the block being modified."
                                                },
                                                expectedOriginalLines = new
                                                {
                                                    type = "array",
                                                    description = "For replace or delete: optional safety check listing the exact original lines expected at [startLine..endLine].",
                                                    items = new { type = "string" }
                                                },
                                                newLines = new
                                                {
                                                    type = "array",
                                                    description = "For insert or replace: the new lines to insert or use as replacement.",
                                                    items = new { type = "string" }
                                                }
                                            },
                                            required = new[] { "operation" }
                                        }
                                    }
                                },
                                required = new[] { "docPath", "originalSha256", "changes" }
                            }
                        }
                    },
                    required = new[] { "files" }
                }
            };
        }

        public const string ToolUsageMetadata = WorkspaceWritePatchOrchestrator.ToolUsageMetadata;
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);

        public Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            return _orchestrator.ExecuteAsync(argumentsJson, context, cancellationToken);
        }
    }
}
