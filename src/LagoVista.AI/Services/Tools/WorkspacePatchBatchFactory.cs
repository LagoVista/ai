using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Services.Tools
{
    public interface IWorkspacePatchBatchFactory
    {
        WorkspacePatchBatch BuildBatch(
            WorkspaceWritePatchArgs args,
            IAgentPipelineContext context,
            Func<string> idGenerator);

        WorkspaceWritePatchResponse BuildResponse(
            WorkspacePatchBatch batch,
            IAgentPipelineContext context);
    }

    /// <summary>
    /// Pure mapping logic: DTOs to domain models to response DTOs.
    /// Fully unit-testable without logger, store, or tool wiring.
    /// </summary>
    public sealed class WorkspacePatchBatchFactory : IWorkspacePatchBatchFactory
    {
        public WorkspacePatchBatch BuildBatch(
            WorkspaceWritePatchArgs args,
            IAgentPipelineContext context,
            Func<string> idGenerator)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            if (idGenerator == null) throw new ArgumentNullException(nameof(idGenerator));

            var batch = new WorkspacePatchBatch
            {
                BatchId = idGenerator(),
                BatchLabel = args.BatchLabel,
                BatchKey = args.BatchKey,
                SessionId = context?.Session.Id,
                Files = new List<WorkspaceFilePatch>()
            };

            foreach (var file in args.Files)
            {
                var filePatch = new WorkspaceFilePatch
                {
                    FilePatchId = idGenerator(),
                    FileKey = file.FileKey,
                    FileLabel = file.FileLabel,
                    DocPath = file.DocPath,
                    OriginalSha256 = file.OriginalSha256,
                    Changes = new List<WorkspaceLineChange>()
                };

                foreach (var change in file.Changes)
                {
                    var op = change.Operation?.Trim().ToLowerInvariant();

                    var changeModel = new WorkspaceLineChange
                    {
                        ChangeId = idGenerator(),
                        ChangeKey = change.ChangeKey,
                        Operation = op,
                        Description = change.Description,
                        AfterLine = change.AfterLine,
                        StartLine = change.StartLine,
                        EndLine = change.EndLine,
                        ExpectedOriginalLines = change.ExpectedOriginalLines?.ToList() ?? new List<string>(),
                        NewLines = change.NewLines?.ToList() ?? new List<string>(),

                        // Planned (future) context-based patching support
                        MatchLines = change.MatchLines?.ToList() ?? new List<string>(),
                        Occurrence = change.Occurrence,
                        MatchMode = change.MatchMode
                    };

                    filePatch.Changes.Add(changeModel);
                }

                batch.Files.Add(filePatch);
            }

            return batch;
        }

        public WorkspaceWritePatchResponse BuildResponse(
            WorkspacePatchBatch batch,
            IAgentPipelineContext context)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));

            var response = new WorkspaceWritePatchResponse
            {
                Success = true,
                ErrorCode = null,
                ErrorMessage = null,
                BatchId = batch.BatchId,
                BatchKey = batch.BatchKey,
                BatchLabel = batch.BatchLabel,
                SessionId = batch.SessionId ?? context.Session.Id,
                Files = new List<WorkspaceWritePatchResponseFile>()
            };

            foreach (var file in batch.Files)
            {
                var fileDto = new WorkspaceWritePatchResponseFile
                {
                    FilePatchId = file.FilePatchId,
                    FileKey = file.FileKey,
                    FileLabel = file.FileLabel,
                    DocPath = file.DocPath,
                    OriginalSha256 = file.OriginalSha256,
                    Changes = new List<WorkspaceWritePatchResponseChange>()
                };

                foreach (var change in file.Changes)
                {
                    var changeDto = new WorkspaceWritePatchResponseChange
                    {
                        ChangeId = change.ChangeId,
                        ChangeKey = change.ChangeKey,
                        Operation = change.Operation,
                        Description = change.Description,
                        AfterLine = change.AfterLine,
                        StartLine = change.StartLine,
                        EndLine = change.EndLine,
                        ExpectedOriginalLines = change.ExpectedOriginalLines?.ToList() ?? new List<string>(),
                        NewLines = change.NewLines?.ToList() ?? new List<string>(),

                        // Planned (future) context-based patching support
                        MatchLines = change.MatchLines?.ToList() ?? new List<string>(),
                        Occurrence = change.Occurrence,
                        MatchMode = change.MatchMode
                    };

                    fileDto.Changes.Add(changeDto);
                }

                response.Files.Add(fileDto);
            }

            return response;
        }
    }

    #region Domain Models

    /// <summary>
    /// Canonical server-side representation of a patch batch.
    /// </summary>
    public sealed class WorkspacePatchBatch
    {
        public string BatchId { get; set; }

        public string BatchLabel { get; set; }

        public string BatchKey { get; set; }

        public string SessionId { get; set; }

        public IList<WorkspaceFilePatch> Files { get; set; } = new List<WorkspaceFilePatch>();
    }

    public sealed class WorkspaceFilePatch
    {
        public string FilePatchId { get; set; }

        public string FileKey { get; set; }

        public string FileLabel { get; set; }

        public string DocPath { get; set; }

        public string OriginalSha256 { get; set; }

        public IList<WorkspaceLineChange> Changes { get; set; } = new List<WorkspaceLineChange>();
    }

    public sealed class WorkspaceLineChange
    {
        public string ChangeId { get; set; }

        public string ChangeKey { get; set; }

        public string Operation { get; set; }

        public string Description { get; set; }

        public int? AfterLine { get; set; }

        public int? StartLine { get; set; }

        public int? EndLine { get; set; }

        public IList<string> ExpectedOriginalLines { get; set; } = new List<string>();

        public IList<string> NewLines { get; set; } = new List<string>();

        // Planned (future) context-based patching support
        public IList<string> MatchLines { get; set; } = new List<string>();

        public string Occurrence { get; set; }

        public string MatchMode { get; set; }
    }

    #endregion

    #region Store Abstraction

    /// <summary>
    /// Abstraction for persisting patch batches so that the client can retrieve and apply them.
    /// Implementation could be in-memory, Cosmos, SQL, etc.
    /// </summary>
    public interface IWorkspacePatchStore
    {
        Task<InvokeResult<WorkspacePatchBatch>> SaveAsync(WorkspacePatchBatch batch, CancellationToken cancellationToken = default);
    }

    public sealed class InMemoryWorkspacePatchStore : IWorkspacePatchStore
    {
        private readonly Dictionary<string, WorkspacePatchBatch> _batches = new Dictionary<string, WorkspacePatchBatch>();

        public Task<InvokeResult<WorkspacePatchBatch>> SaveAsync(
            WorkspacePatchBatch batch,
            CancellationToken cancellationToken = default)
        {
            _batches[batch.BatchId] = batch;
            return Task.FromResult(InvokeResult<WorkspacePatchBatch>.Create(batch));
        }
    }

    #endregion

    #region Response DTOs

    /// <summary>
    /// JSON payload returned from workspace_write_patch.
    /// This is what the client (and LLM) see inside the tool result.
    /// </summary>
    public sealed class WorkspaceWritePatchResponse
    {
        public bool Success { get; set; }

        public string ErrorCode { get; set; }

        public string ErrorMessage { get; set; }

        public string BatchId { get; set; }

        public string BatchKey { get; set; }

        public string BatchLabel { get; set; }

        public string SessionId { get; set; }

        public List<WorkspaceWritePatchResponseFile> Files { get; set; } = new List<WorkspaceWritePatchResponseFile>();
    }

    public sealed class WorkspaceWritePatchResponseFile
    {
        public string FilePatchId { get; set; }

        public string FileKey { get; set; }

        public string FileLabel { get; set; }

        public string DocPath { get; set; }

        public string OriginalSha256 { get; set; }

        public List<WorkspaceWritePatchResponseChange> Changes { get; set; } = new List<WorkspaceWritePatchResponseChange>();
    }

    public sealed class WorkspaceWritePatchResponseChange
    {
        public string ChangeId { get; set; }

        public string ChangeKey { get; set; }

        public string Operation { get; set; }

        public string Description { get; set; }

        public int? AfterLine { get; set; }

        public int? StartLine { get; set; }

        public int? EndLine { get; set; }

        public List<string> ExpectedOriginalLines { get; set; } = new List<string>();

        public List<string> NewLines { get; set; } = new List<string>();

        // Planned (future) context-based patching support
        public List<string> MatchLines { get; set; } = new List<string>();

        public string Occurrence { get; set; }

        public string MatchMode { get; set; }
    }

    #endregion
}
