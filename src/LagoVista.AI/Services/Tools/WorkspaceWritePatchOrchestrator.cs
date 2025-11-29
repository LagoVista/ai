using System;
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
    /// Orchestrates deserialization, validation, id assignment, store calls and
    /// response building for workspace_write_patch.
    /// This is the main class to unit test.
    /// </summary>
    public interface IWorkspaceWritePatchOrchestrator
    {
        Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default);
    }

    public sealed class WorkspaceWritePatchOrchestrator : IWorkspaceWritePatchOrchestrator
    {
        public const string ToolUsageMetadata = @"workspace_write_patch — Usage Guide

Primary purpose:
- Capture a multi-file, line-based patch batch for one or more workspace files.
- Each patch batch is stored server-side with stable ids so the client can later:
  - validate SHA256 against local files, and
  - apply the exact line changes you described.

Rules:
- Always compute a SHA256 of the full file content before proposing any edits.
- Always send the SHA256 you computed in originalSha256 for each file.
- Always treat docPath as the canonical identifier for the file. It is lower-case and unique.
- Only propose full-line operations (insert, replace, delete).
- Do not send partial-line edits. Always send full lines.
- Prefer fewer, larger line blocks over many tiny changes when they are logically related.
- You may patch multiple files in a single call by adding more entries to files[].
- You should provide a short description for each change so humans can quickly review.

Line operations:
- Insert:
  - operation: ""insert""
  - Use afterLine to indicate the 1-based line AFTER which the newLines should be inserted.
  - Use afterLine = 0 to insert at the very top of the file.
  - Provide newLines with the exact lines to insert.
- Replace:
  - operation: ""replace""
  - Use startLine and endLine (inclusive) to indicate the block to replace.
  - Provide newLines with the replacement lines.
  - Optionally include expectedOriginalLines to help the client validate.
- Delete:
  - operation: ""delete""
  - Use startLine and endLine (inclusive) to indicate the block to remove.
  - Optionally include expectedOriginalLines to help the client validate.

Ids and keys:
- The server will assign:
  - batchId for the whole batch,
  - filePatchId for each file,
  - changeId for each individual change.
- You may provide batchKey, fileKey, and changeKey if you want stable labels you can reuse in later turns.
- When asking follow-up questions about a specific change, always reference the ids or keys from the previous tool result.

When to use:
- Use this tool whenever you want the client to apply concrete code changes to one or more files.
- Good examples:
  - Add tests for AgentOrchestrator (multiple new files).
  - Refactor RagAnswerService to use the responses API (multi-block edits).
- Do not use this tool just to explore or read files; use workspace_read_file and RAG search tools for that.

Error behavior:
- If arguments are invalid (missing docPath, SHA, or changes), the tool returns Success = false with an ErrorCode.
- If internal persistence fails, the tool returns Success = false with ErrorCode = ""STORE_ERROR"".
- The tool never throws across the wire; errors are encoded in the JSON payload.

Good usage:
- Prepare a plan, read the relevant files, compute SHA256, then call workspace_write_patch with:
  - clear docPath and SHA per file,
  - minimal but coherent sets of changes,
  - concise descriptions for each change.

Bad usage:
- Calling workspace_write_patch without first computing SHA256.
- Attempting character-level edits inside a line.
- Spamming many tiny patches when a single coherent patch would suffice.
";

        private readonly IAdminLogger _logger;
        private readonly IWorkspacePatchStore _patchStore;
        private readonly IWorkspaceWritePatchValidator _validator;
        private readonly IWorkspacePatchBatchFactory _batchFactory;

        public WorkspaceWritePatchOrchestrator(
            IAdminLogger logger,
            IWorkspacePatchStore patchStore,
            IWorkspaceWritePatchValidator validator,
            IWorkspacePatchBatchFactory batchFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _patchStore = patchStore ?? throw new ArgumentNullException(nameof(patchStore));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _batchFactory = batchFactory ?? throw new ArgumentNullException(nameof(batchFactory));
        }

        public async Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                var cancelledPayload = JsonConvert.SerializeObject(new WorkspaceWritePatchResponse
                {
                    Success = false,
                    ErrorCode = "CANCELLED",
                    ErrorMessage = "workspace_write_patch execution was cancelled before processing.",
                    SessionId = context?.SessionId,
                    ConversationId = context?.Request?.ConversationId
                });

                return InvokeResult<string>.Create(cancelledPayload);
            }

            try
            {
                if (string.IsNullOrWhiteSpace(argumentsJson))
                {
                    var missingPayload = JsonConvert.SerializeObject(new WorkspaceWritePatchResponse
                    {
                        Success = false,
                        ErrorCode = "MISSING_ARGUMENTS",
                        ErrorMessage = "workspace_write_patch requires a non-empty JSON arguments payload.",
                        SessionId = context?.SessionId,
                        ConversationId = context?.Request?.ConversationId
                    });

                    return InvokeResult<string>.Create(missingPayload);
                }

                WorkspaceWritePatchArgs args;
                try
                {
                    args = JsonConvert.DeserializeObject<WorkspaceWritePatchArgs>(argumentsJson)
                           ?? new WorkspaceWritePatchArgs();
                }
                catch (Exception ex)
                {
                    _logger.AddException("[workspace_write_patch_Deserialize]", ex);

                    var errorPayload = JsonConvert.SerializeObject(new WorkspaceWritePatchResponse
                    {
                        Success = false,
                        ErrorCode = "DESERIALIZATION_ERROR",
                        ErrorMessage = "Unable to deserialize workspace_write_patch arguments.",
                        SessionId = context?.SessionId,
                        ConversationId = context?.Request?.ConversationId
                    });

                    return InvokeResult<string>.Create(errorPayload);
                }

                var validationResult = _validator.Validate(args);
                if (!validationResult.Successful)
                {
                    var validationPayload = JsonConvert.SerializeObject(new WorkspaceWritePatchResponse
                    {
                        Success = false,
                        ErrorCode = "VALIDATION_FAILED",
                        ErrorMessage = validationResult.ErrorMessage,
                        SessionId = context?.SessionId,
                        ConversationId = context?.Request?.ConversationId
                    });

                    return InvokeResult<string>.Create(validationPayload);
                }

                var batch = _batchFactory.BuildBatch(args, context, NewId);

                var storeResult = await _patchStore.SaveAsync(batch, cancellationToken);
                if (!storeResult.Successful)
                {
                    var storePayload = JsonConvert.SerializeObject(new WorkspaceWritePatchResponse
                    {
                        Success = false,
                        ErrorCode = "STORE_ERROR",
                        ErrorMessage = storeResult.ErrorMessage,
                        SessionId = context?.SessionId,
                        ConversationId = context?.Request?.ConversationId
                    });

                    return InvokeResult<string>.Create(storePayload);
                }

                var storedBatch = storeResult.Result ?? batch;
                var responseDto = _batchFactory.BuildResponse(storedBatch, context);
                var json = JsonConvert.SerializeObject(responseDto);

                return InvokeResult<string>.Create(json);
            }
            catch (Exception ex)
            {
                _logger.AddException("[workspace_write_patch_Execute]", ex);

                var errorPayload = JsonConvert.SerializeObject(new WorkspaceWritePatchResponse
                {
                    Success = false,
                    ErrorCode = "UNEXPECTED_ERROR",
                    ErrorMessage = "workspace_write_patch failed to process arguments.",
                    SessionId = context?.SessionId,
                    ConversationId = context?.Request?.ConversationId
                });

                return InvokeResult<string>.Create(errorPayload);
            }
        }

        private static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
