using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Services
{
    /// <summary>
    /// Default implementation of <see cref="IIndexFileContextBuilder"/>.
    ///
    /// For each <see cref="PlannedFileIngestion"/>, this builder:
    ///  - Computes the full file path from Ingestion.SourceRoot + repoId + CanonicalPath.
    ///  - Computes a normalized content hash using <see cref="ContentHashUtil"/>.
    ///  - Builds a canonical <see cref="DocumentIdentity"/> (DocId) using OrgId/RepoId/RelativePath.
    ///  - Synchronizes the corresponding <see cref="LocalIndexRecord"/> in <see cref="LocalIndexStore"/>.
    ///  - Returns an <see cref="IndexFileContext"/> populated with identity and metadata.
    /// </summary>
    public sealed class IndexFileContextBuilder : IIndexFileContextBuilder
    {
        IIndexIdServices _indexIdServices;
        public IndexFileContextBuilder(IIndexIdServices indexIdServices)
        {
            _indexIdServices = indexIdServices ?? throw new ArgumentNullException(nameof(indexIdServices));
        }

        public async Task<IndexFileContext> BuildAsync(
            IngestionConfig config,
            GitRepoInfo gitRepoInfo,
            string repoId,
            PlannedFileIngestion plannedFile,
            LocalIndexStore localIndex,
            CancellationToken token = default)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(repoId)) throw new ArgumentNullException(nameof(repoId));
            if (plannedFile == null) throw new ArgumentNullException(nameof(plannedFile));
            if (localIndex == null) throw new ArgumentNullException(nameof(localIndex));

            if (string.IsNullOrWhiteSpace(config.Ingestion?.SourceRoot))
                throw new InvalidOperationException("Ingestion.SourceRoot must be configured.");

            if (string.IsNullOrWhiteSpace(plannedFile.CanonicalPath))
                throw new InvalidOperationException("PlannedFileIngestion.CanonicalPath is required.");

            token.ThrowIfCancellationRequested();

            var orgId = config.OrgId;
            var projectId = config.ContentRepo?.AccountId; // optional

            var repoRoot = Path.Combine(config.Ingestion.SourceRoot, repoId);

            var relativePath = plannedFile.CanonicalPath.Replace('\\', '/');
            var fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found for indexing context: {fullPath}", fullPath);
            }

            var contentHash = await ContentHashUtil.ComputeFileContentHashAsync(fullPath, token);

            var identity = new DocumentIdentity
            {
                OrgId = orgId,
                OrgNamespace = config.OrgNamespace,
                ProjectId = projectId,
                RepoId = repoId,
                RelativePath = relativePath,
            };

            

            identity.DocId = _indexIdServices.ComputeDocId(gitRepoInfo.RemoteUrl, projectId, relativePath);
           
            var localRecord = localIndex.GetOrAdd(relativePath, identity.DocId);
            localRecord.ActiveContentHash = contentHash;

            if (string.IsNullOrWhiteSpace(localRecord.ContentHash))
            {
                localRecord.ContentHash = contentHash;
            }

            localRecord.DocId = identity.DocId;

            if (!string.IsNullOrWhiteSpace(plannedFile.SubKind))
            {
                localRecord.SubKind = plannedFile.SubKind;
            }

            if (string.IsNullOrWhiteSpace(localRecord.Reindex) && !string.IsNullOrWhiteSpace(plannedFile.Reindex))
            {
                localRecord.Reindex = plannedFile.Reindex;
            }

            var ctx = new IndexFileContext
            {
                FullPath = fullPath,
                RelativePath = relativePath,
                Language = DetectLanguage(relativePath),
                DocumentIdentity = identity,
                GitRepoInfo = gitRepoInfo,
                RepoId = repoId,
                BlobUri = $"{repoId}/{relativePath}",
            };

            return ctx;
        }

        private static string DetectLanguage(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;

            var ext = Path.GetExtension(relativePath).ToLowerInvariant();
            switch (ext)
            {
                case ".cs":
                    return "csharp";
                case ".json":
                    return "json";
                case ".resx":
                    return "resx";
                case ".xml":
                    return "xml";
                default:
                    return null;
            }
        }
    }
}
