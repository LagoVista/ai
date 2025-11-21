using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Services
{
    /// <summary>
    /// Default implementation of <see cref="IFileChunkingService"/>.
    ///
    /// Mode 1: symbol-centric chunking using <see cref="IChunkerServices.DetectForFile"/>.
    ///
    /// For each <see cref="SubKindDetectionResult"/> returned by the chunker, this service
    /// produces a <see cref="NormalizedChunk"/> that:
    ///  - Has a canonical <see cref="DocumentIdentity"/> (DocId + SectionKey + ChunkId).
    ///  - Includes a standard header (Org/Project/Repo/Path/SubKind/Symbol).
    ///  - Includes an optional human-readable summary (result.Summary).
    ///  - Includes the symbol-level code text (result.SymbolText).
    /// </summary>
    public sealed class DefaultNormalizedChunkBuilderService : INormalizedChunkBuilder
    {
        private readonly IChunkerServices _chunkerServices;

        public DefaultNormalizedChunkBuilderService(IChunkerServices chunkerServices)
        {
            _chunkerServices = chunkerServices ?? throw new ArgumentNullException(nameof(chunkerServices));
        }

        public async Task<IReadOnlyList<NormalizedChunk>> BuildChunksAsync(
            IndexFileContext fileContext,
            CancellationToken token = default)
        {
            if (fileContext == null) throw new ArgumentNullException(nameof(fileContext));
            if (string.IsNullOrWhiteSpace(fileContext.FullPath))
                throw new ArgumentException("IndexFileContext.FullPath is required.", nameof(fileContext));

            token.ThrowIfCancellationRequested();

            if (!File.Exists(fileContext.FullPath))
            {
                throw new FileNotFoundException(
                    $"Source file not found for chunking: {fileContext.FullPath}",
                    fileContext.FullPath);
            }

            var sourceText = await File.ReadAllTextAsync(fileContext.FullPath, token)
                .ConfigureAwait(false);

            var relativePath = (fileContext.RelativePath ?? fileContext.FullPath)
                .Replace('\\', '/');

            var result = _chunkerServices.DetectForFile(sourceText, relativePath);   
            var chunks = new List<NormalizedChunk>();
            var index = 0;

            token.ThrowIfCancellationRequested();

            var symbolName = string.IsNullOrWhiteSpace(result.PrimaryTypeName)
                ? $"symbol-{index}"
                : result.PrimaryTypeName;

            var identity = new DocumentIdentity
            {
                OrgId = fileContext.OrgId,
                ProjectId = fileContext.ProjectId,
                RepoId = fileContext.RepoId,
                RelativePath = relativePath,
                Symbol = symbolName,
                SymbolType = result.SubKindString
            };

            identity.ComputeDocId();

            var sectionKeyBase = symbolName.ToLowerInvariant();
            identity.SectionKey = $"symbol-{sectionKeyBase}";
            identity.ComputeChunkId();

            var sb = new StringBuilder();

            sb.AppendLine($"OrgId: {fileContext.OrgId}");
            sb.AppendLine($"ProjectId: {fileContext.ProjectId}");
            sb.AppendLine($"RepoId: {fileContext.RepoId}");
            sb.AppendLine($"Path: {relativePath}");
            sb.AppendLine($"SubKind: {result.SubKindString}");
            sb.AppendLine($"Symbol: {symbolName}");

            if (!string.IsNullOrWhiteSpace(result.Summary))
            {
                sb.AppendLine();
                sb.AppendLine("Summary:");
                sb.AppendLine(result.Summary.Trim());
            }

            if (!string.IsNullOrWhiteSpace(result.Reason))
            {
                sb.AppendLine();
                sb.AppendLine("Detection Reason:");
                sb.AppendLine(result.Reason.Trim());
            }

            if (!string.IsNullOrWhiteSpace(result.SymbolText))
            {
                sb.AppendLine();
                sb.AppendLine("Code:");
                sb.AppendLine(result.SymbolText.Trim());
            }

            var chunk = new NormalizedChunk
            {
                Identity = identity,
                Kind = "SourceCode",
                SubKind = result.SubKindString,
                NormalizedText = sb.ToString()
            };

            chunks.Add(chunk);
            index++;
            

            return chunks;
        }
    }
}
