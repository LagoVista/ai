using System;
using System.Linq;
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
    /// Orchestrates deserialization, validation, id assignment, store calls and
    /// response building for workspace_write_patch.
    /// This is the main class to unit test.
    /// </summary>
    public interface IWorkspaceWritePatchOrchestrator
    {
        Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            IAgentPipelineContext context,
            CancellationToken cancellationToken = default);
    }

    public sealed class WorkspaceWritePatchOrchestrator : IWorkspaceWritePatchOrchestrator
    {
        public const string ToolUsageMetadata = @"workspace_write_patch — Usage Guide

Primary purpose:
- Capture a multi-file patch batch for one or more workspace files.
- The server validates and stores the patch batch; the client later applies it to local files.

Key rules:
- Always compute a SHA256 of the full file content before proposing any edits.
- Always send the SHA256 you computed in originalSha256 for each file (required).
- Always treat docPath as the canonical identifier for the file. It is lower-case and unique.
- Only propose full-line operations. Do not send partial-line edits.

Safety/validation notes:
- originalSha256 is required per file and is used to detect drift before applying patches.
- expectedOriginalLines is a safety guard for replaceByRange/delete: it is NOT a search key.
  It is validated against the exact [startLine..endLine] range.

Operations:
- Insert:
  - operation: ""insert""
  - Use afterLine to indicate the 1-based line number AFTER which newLines should be inserted.
  - Use afterLine = 0 to insert at the very top of the file.
  - Provide newLines with the exact lines to insert.

- Delete:
  - operation: ""delete""
  - Use startLine and endLine (inclusive) to indicate the block to remove.
  - expectedOriginalLines (optional but recommended): exact original lines expected at [startLine..endLine].

- Replace (context-based):
  - operation: ""replace""
  - Provide matchLines (the exact contiguous block of lines to find) and newLines (replacement).
  - Optional: occurrence (single|first|last), matchMode (ignoreLineEndings|exact)
  - This is resilient to line-number drift.

- ReplaceByRange (line-based):
  - operation: ""replaceByRange""
  - Use startLine and endLine (inclusive) to indicate the block to replace.
  - Provide newLines with the replacement lines.
  - expectedOriginalLines (optional but recommended): exact original lines expected at [startLine..endLine].

Ids and keys:
- The server will assign:
  - batchId for the whole batch,
  - filePatchId for each file,
  - changeId for each individual change.
- You may provide batchKey, fileKey, and changeKey if you want stable labels you can reuse in later turns.

Error behavior:
- If arguments are invalid (missing docPath, SHA, or changes), the tool returns Success = false with an ErrorCode.
- If internal persistence fails, the tool returns Success = false with ErrorCode = ""STORE_ERROR"".
- The tool never throws across the wire; errors are encoded in the JSON payload.
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
            IAgentPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                var cancelledPayload = JsonConvert.SerializeObject(new WorkspaceWritePatchResponse
                {
                    Success = false,
                    ErrorCode = "CANCELLED",
                    ErrorMessage = "workspace_write_patch execution was cancelled before processing.",
                    SessionId = context.Session.Id,
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
                        SessionId = context.Session.Id,
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
                    _logger.AddException(this.Tag(), ex);

                    var errorPayload = JsonConvert.SerializeObject(new WorkspaceWritePatchResponse
                    {
                        Success = false,
                        ErrorCode = "DESERIALIZATION_ERROR",
                        ErrorMessage = "Unable to deserialize workspace_write_patch arguments.",
                        SessionId = context.Session.Id,
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
                        SessionId = context.Session.Id,
                    });

                    return InvokeResult<string>.Create(validationPayload);
                }

                var batch = _batchFactory.BuildBatch(args, context, NewId);

                foreach(var file in batch.Files)
                {
                    var existingFile = context.Session.TouchedFiles.FirstOrDefault(f => f.Path == file.DocPath);

                    if(existingFile != null)
                    {
                        existingFile.ContentHash = file.OriginalSha256;
                        existingFile.LastAccess = DateTime.UtcNow.ToJSONString();
                    }
                    else 
                    {
                        context.Session.TouchedFiles.Add(new TouchedFile()
                        {
                            Path = file.DocPath,
                            ContentHash = file.OriginalSha256,
                            LastAccess = DateTime.UtcNow.ToJSONString(),        
                        });
                    }
                }

                var storeResult = await _patchStore.SaveAsync(batch, cancellationToken);
                if (!storeResult.Successful)
                {
                    var storePayload = JsonConvert.SerializeObject(new WorkspaceWritePatchResponse
                    {
                        Success = false,
                        ErrorCode = "STORE_ERROR",
                        ErrorMessage = storeResult.ErrorMessage,
                        SessionId = context.Session.Id,

                    });

                    return InvokeResult<string>.Create(storePayload);
                }

                var storedBatch = storeResult.Result ?? batch;
                var responseDto = _batchFactory.BuildResponse(storedBatch, context);
                var json = JsonConvert.SerializeObject(responseDto);

                _logger.Trace($"[JSON.FilePatch]={json}");

                return InvokeResult<string>.Create(json);
            }
            catch (Exception ex)
            {
                _logger.AddException(this.Tag(), ex);

                var errorPayload = JsonConvert.SerializeObject(new WorkspaceWritePatchResponse
                {
                    Success = false,
                    ErrorCode = "UNEXPECTED_ERROR",
                    ErrorMessage = "workspace_write_patch failed to process arguments.",
                    SessionId = context?.Session.Id,
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
