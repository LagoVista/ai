using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Builds an IngestionPlan for a single repository.
    ///
    /// Responsibilities:
    /// - For each discovered file: compute ActiveContentHash, update LocalIndexStore, and decide whether to index.
    /// - Detect missing files in the local index and schedule DocId deletions (IDX-0035).
    /// - Attach DocId and canonical path using IndexIds (IDX-001, IDX-003).
    ///
    /// This class does not talk to Qdrant or the LLM; it only builds the plan.
    /// </summary>
    public class FileIngestionPlanner
    {
        /// <summary>
        /// Build an ingestion plan for a repository.
        ///
        /// repoRoot        - physical repo root on disk.
        /// projectId       - logical project id (e.g., co.core) used in canonical paths.
        /// config          - IngestionConfig controlling global reindex flag etc.
        /// repoInfo        - Git metadata (remote URL, branch, commit).
        /// localIndex      - loaded LocalIndexStore for this repo.
        /// currentFiles    - full paths of files discovered on disk for this repo.
        /// </summary>
        public async Task<FileIngestionPlan> BuildPlanAsync(string repoRoot, string projectId, IngestionConfig config, RepoInfo repoInfo, LocalIndexStore localIndex, IEnumerable<string> currentFiles, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(repoRoot)) throw new ArgumentNullException(nameof(repoRoot));
            if (string.IsNullOrWhiteSpace(projectId)) throw new ArgumentNullException(nameof(projectId));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (repoInfo == null) throw new ArgumentNullException(nameof(repoInfo));
            if (localIndex == null) throw new ArgumentNullException(nameof(localIndex));

            var plan = new FileIngestionPlan()
            {
                RepoRoot = repoRoot,
                RepoUrl = repoInfo.RemoteUrl,
                BranchRef = repoInfo.BranchRef,
                ProjectId = projectId
            };

            var currentList = (currentFiles ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // 1) For each file on disk, compute ActiveContentHash and decide whether it should be indexed.
            foreach (var fullPath in currentList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath)) continue;

                var relativePath = Path.GetRelativePath(repoRoot, fullPath);
                var canonicalPath = IndexIds.BuildCanonicalPath(projectId, relativePath);

                var hash = await ContentHashUtil.ComputeFileContentHashAsync(fullPath, cancellationToken);
                localIndex.UpdateActiveContentHash(canonicalPath, hash);

                var record = localIndex.GetOrAdd(canonicalPath);
                var hasIndexedBefore = !string.IsNullOrWhiteSpace(record.ContentHash);

                // Global reindex flag forces reindex of everything.
                var forcedReindex = Convert.ToBoolean(config.Reindex);
                
                // Per-file directive.
                var explicitReindex = record.Reindex == "chunk" || record.Reindex == "full";

                // Hash mismatch means content changed.
                var contentChanged = !string.Equals(record.ContentHash, hash, StringComparison.OrdinalIgnoreCase);

                var shouldIndex = forcedReindex || explicitReindex || !hasIndexedBefore || contentChanged;
                if (!shouldIndex) continue;

                // Ensure DocId exists for this file.
                if (string.IsNullOrWhiteSpace(record.DocId) && !string.IsNullOrWhiteSpace(repoInfo.RemoteUrl))
                {
                    record.DocId = IndexIds.ComputeDocId(repoInfo.RemoteUrl, canonicalPath);
                }

                var planned = new PlannedFileIngestion
                {
                    RepoRoot = repoRoot,
                    ProjectId = projectId,
                    RepoUrl = repoInfo.RemoteUrl,
                    CanonicalPath = canonicalPath,
                    DocId = record.DocId,
                    LocalPath = fullPath,
                    Reindex = record.Reindex,
                    IsActive = record.IsActive || forcedReindex || explicitReindex,
                    IsNewDocument = !hasIndexedBefore
                };

                plan.FilesToIndex.Add(planned);
            }

            // 2) Detect records whose files are missing from disk and schedule DocId deletion.
            var currentCanonical = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fullPath in currentList)
            {
                if (string.IsNullOrWhiteSpace(fullPath)) continue;
                if (!File.Exists(fullPath)) continue;

                var relativePath = Path.GetRelativePath(repoRoot, fullPath);
                var canonicalPath = IndexIds.BuildCanonicalPath(projectId, relativePath);
                currentCanonical.Add(canonicalPath);
            }

            foreach (var record in localIndex.Records)
            {
                if (string.IsNullOrWhiteSpace(record.FilePath)) continue;
                if (currentCanonical.Contains(record.FilePath)) continue;

                if (!string.IsNullOrWhiteSpace(record.DocId) && !string.IsNullOrWhiteSpace(repoInfo.RemoteUrl))
                {
                    plan.DocsToDelete.Add(new PlannedDocDeletion
                    {
                        RepoUrl = repoInfo.RemoteUrl,
                        DocId = record.DocId,
                        CanonicalPath = record.FilePath
                    });
                }
            }

            return plan;
        }
    }
}
