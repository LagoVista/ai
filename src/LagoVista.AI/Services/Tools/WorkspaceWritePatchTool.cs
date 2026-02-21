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

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Capture a multi-file, line-based patch batch for one or more workspace files, including SHA256 guards and change descriptions. The client will later apply these patches to local files.",
                p =>
                {
                    p.String("batchLabel", "Optional human-readable label for this patch batch (for example: Add tests for AgentOrchestrator).");
                    p.String("batchKey", "Optional LLM-chosen stable key for this batch so the LLM can reference it later (for example: agent-orchestrator-tests-v1).");
                    p.String("reason", "Provide a one or two sentance reason as to why this file was added or changed. This will be used to keep track of all the files that have been touched during this agent session. ");
                    p.ObjectArray("files", "One or more file patch descriptors.",
                        file =>
                        {
                            file.String("docPath", "Canonical path identifying the file (always lower-case; matches DocPath and Active File List semantics).", required: true);
                            file.String("fileKey", "Optional LLM-chosen stable key for this file within the batch (for example: orchestrator-tests).");
                            file.String("fileLabel", "Optional human-readable label for this file patch (for example: AgentOrchestratorTests.cs).");

                            var sha = new JsonSchemaProperty
                            {
                                Type = "string",
                                Description = "SHA256 of the file content the LLM used when preparing this patch. Used by the client to detect drift before applying."
                            };
                            sha.MinLength(64);
                            sha.MaxLength(64);
                            file.Properties["originalSha256"] = sha;
                            file.Require("docPath", "originalSha256");

                            file.ObjectArray("changes", "Ordered list of line-based changes to apply to this file.",
                                change =>
                                {
                                    change.String("changeKey", "Optional LLM-chosen stable key for this change, used to ask follow-up questions or request modifications.");

                                    // Operation names:
                                    // - insert
                                    // - delete
                                    // - replace: context-based replace (formerly replaceByMatch)
                                    // - replaceByRange: line-range replace (formerly replace)
                                    change.String("operation", "Type of operation.", enumValues: new[] { "insert", "delete", "replace", "replaceByRange" }, required: true);

                                    change.String("description", "Short explanation of why this change is being made. Keep it concise but meaningful.");

                                    // Range-based fields
                                    change.Integer("afterLine", "For insert: 1-based line number AFTER which newLines should be inserted. Use 0 to insert at top of file.");
                                    change.Integer("startLine", "For replaceByRange or delete: 1-based starting line number (inclusive) of the block being modified.");
                                    change.Integer("endLine", "For replaceByRange or delete: 1-based ending line number (inclusive) of the block being modified.");
                                    change.StringArray("expectedOriginalLines", "For replaceByRange or delete: optional safety check listing the exact original lines expected at [startLine..endLine].");
                                    change.StringArray("newLines", "For insert, replace, or replaceByRange: the new lines to insert or use as replacement.");

                                    // Context-based replace fields
                                    change.StringArray("matchLines", "For replace: the exact contiguous block of lines to find in the file.");
                                    change.String("occurrence", "For replace: how to choose match if multiple.", enumValues: new[] { "single", "first", "last" });
                                    change.String("matchMode", "For replace: matching behavior.", enumValues: new[] { "ignoreLineEndings", "exact" });
                                },
                                required: true,
                                minItems: 1);

                            var changesProp = file.Properties["changes"];
                            var changeItemNode = changesProp.Items;

                            changeItemNode.OneOf(b =>
                            {
                                b.Operation("insert", required: new[] { "afterLine", "newLines" });
                                b.Operation("delete", required: new[] { "startLine", "endLine" });
                                b.Operation("replace", required: new[] { "matchLines", "newLines" });
                                b.Operation("replaceByRange", required: new[] { "startLine", "endLine", "newLines" });
                            });
                        },
                        required: true,
                        minItems: 1);
                });
        }

        public const string ToolUsageMetadata = WorkspaceWritePatchOrchestrator.ToolUsageMetadata;
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            return _orchestrator.ExecuteAsync(argumentsJson, context, context.CancellationToken);
        } 
        
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            // This tool is executed via the IAgentPipelineContext overload.
            return Task.FromResult(InvokeResult<string>.FromError("not_supported"));      
        }
    }
}
