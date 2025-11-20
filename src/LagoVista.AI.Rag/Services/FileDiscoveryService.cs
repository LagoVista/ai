using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Types;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Discovers files for a repository, updates the LocalIndexStore with ActiveContentHash,
    /// and determines which files need indexing or have gone missing.
    ///
    /// This is the bridge between:
    ///   - FileWalker (physical files)
    ///   - LocalIndexStore (IDX-0036)
    ///   - IndexIds canonical path rules (IDX-001/IDX-003)
    /// </summary>
    public class FileDiscoveryService
    {
        /// <summary>
        /// Result of a discovery pass for a single repository.
        /// </summary>
        public class DiscoveryResult
        {
            /// <summary>
            /// Canonical paths (per IndexIds.BuildCanonicalPath) for files that should be (re)indexed.
            /// </summary>
            public List<string> FilesToIndex { get; } = new List<string>();

            /// <summary>
            /// Records whose FilePath no longer exists on disk (or is filtered out).
            /// </summary>
            public List<LocalIndexRecord> MissingRecords { get; } = new List<LocalIndexRecord>();

            /// <summary>
            /// All records after ActiveContentHash has been updated for this run.
            /// </summary>
            public List<LocalIndexRecord> AllRecords { get; } = new List<LocalIndexRecord>();
        }

        /// <summary>
        /// Discover files under a single repository root, update the local index, and compute
        /// which files require (re)indexing.
        ///
        /// - repoRoot: physical directory on disk for this repo (e.g. D:\\nuviot\\co.core)
        /// - projectId: logical project identifier (e.g. "co.core") used in canonical paths
        /// - config: global ingestion config (for include/exclude globs)
        /// - localIndex: per-repo LocalIndexStore
        /// </summary>
        public async Task<DiscoveryResult> DiscoverAsync(
            string repoRoot,
            string projectId,
            IngestionConfig config,
            LocalIndexStore localIndex,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(repoRoot)) throw new ArgumentNullException(nameof(repoRoot));
            if (string.IsNullOrWhiteSpace(projectId)) throw new ArgumentNullException(nameof(projectId));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (localIndex == null) throw new ArgumentNullException(nameof(localIndex));

            var result = new DiscoveryResult();

            if (!Directory.Exists(repoRoot))
            {
                return result;
            }

            // 1) Enumerate physical files using FileWalker and the configured globs.
            var files = FileWalker.EnumerateFiles(
                repoRoot,
                config.Ingestion?.Include,
                config.Ingestion?.Exclude) ?? Enumerable.Empty<string>();

            var currentCanonicalPaths = new List<string>();

            foreach (var fullPath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Repo-relative path (for canonical path and DocId purposes)
                var relPath = Path.GetRelativePath(repoRoot, fullPath);

                // Canonical path per IDX-003 (projectId + path in repo)
                var canonicalPath = IndexIds.BuildCanonicalPath(projectId, relPath);
                currentCanonicalPaths.Add(canonicalPath);

                // Compute ActiveContentHash for the current on-disk content
                var contentHash = await ContentHashUtil
                    .ComputeFileContentHashAsync(fullPath, cancellationToken)
                    .ConfigureAwait(false);

                // Update the local index with the active hash
                localIndex.UpdateActiveContentHash(canonicalPath, contentHash);
            }

            // 2) Determine missing/orphaned records (present in index, not on disk / not discovered)
            var missing = localIndex.GetMissingFiles(currentCanonicalPaths).ToList();
            result.MissingRecords.AddRange(missing);

            // 3) Determine which files need (re)indexing.
            //    A file needs indexing when:
            //      - It has no ContentHash yet (never indexed), OR
            //      - Reindex flag is set ("chunk" or "full"), OR
            //      - ActiveContentHash != ContentHash (record.IsActive).
            foreach (var record in localIndex.Records)
            {
                result.AllRecords.Add(record);

                var needsReindexFlag = !string.IsNullOrWhiteSpace(record.Reindex);
                var neverIndexed = string.IsNullOrWhiteSpace(record.ContentHash);
                var isActive = record.IsActive;

                if (needsReindexFlag || neverIndexed || isActive)
                {
                    // Use FilePath (canonical path) as the identity that flows into the rest of the pipeline.
                    if (!string.IsNullOrWhiteSpace(record.FilePath))
                    {
                        result.FilesToIndex.Add(record.FilePath);
                    }
                }
            }

            return result;
        }
    }
}
