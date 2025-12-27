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
            return ToolSchema.Function(ToolName, "Capture a multi-file, line-based patch batch for one or more workspace files, including SHA256 guards and change descriptions. The client will later apply these patches to local files.", p =>
            {
                p.String("batchLabel", "Optional human-readable label for this patch batch (for example: Add tests for AgentOrchestrator).");
                p.String("batchKey", "Optional LLM-chosen stable key for this batch so the LLM can reference it later (for example: agent-orchestrator-tests-v1).");
                p.Any("files", "array", "One or more file patch descriptors.", required: true);
            });
        }

        public const string ToolUsageMetadata = WorkspaceWritePatchOrchestrator.ToolUsageMetadata;
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            return _orchestrator.ExecuteAsync(argumentsJson, context, cancellationToken);
        }
    }
}