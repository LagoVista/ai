using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.AI.Rag.Types;
using LagoVista.AI.Services;
using LagoVista.Core;
using LagoVista.IoT.Logging;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// High-level coordinator for a single indexing run.
    ///
    /// Responsibilities (library-only, no console / UI):
    /// - Walk configured repositories from <see cref="IngestionConfig"/>.
    /// - For each repo:
    ///   - Discover files to consider.
    ///   - Load local-index.json via <see cref="LocalIndexStore"/>.
    ///   - Compute ActiveContentHash for each file via <see cref="ContentHashUtil"/>.
    ///   - Identify missing files and delegate deletion to the pipeline.
    ///   - Decide which files should be (re)indexed.
    ///   - Delegate actual indexing work to an injected <see cref="IIndexingPipeline"/>.
    /// - Persist local-index.json after each change for crash-safe behavior.
    ///
    /// This class is intentionally free of Qdrant, OpenAI, or blob-storage specifics.
    /// Those live behind <see cref="IIndexingPipeline"/>, so multiple front-ends
    /// (console app, background worker, tests) can reuse the same orchestration logic.
    /// </summary>
    public class IndexRunOrchestrator
    {
        private readonly IngestionConfig _config;
        private readonly AgentContext _agentContext;
        private readonly IAdminLogger _logger;
        private readonly IIndexingPipeline _pipeline;

        public IndexRunOrchestrator(IngestionConfig config, AgentContext agentContext, IAdminLogger logger, IIndexingPipeline pipeline)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _agentContext = agentContext ?? throw new ArgumentNullException(nameof(agentContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }

        /// <summary>
        /// Execute a complete indexing run across all configured repositories.
        /// </summary>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            if (_config.Ingestion == null) throw new InvalidOperationException("Ingestion config is missing.");

            var repos = _config.Ingestion.Repositories ?? new List<string>();
            if (repos.Count == 0)
            {
                _logger.Trace("[IndexRunOrchestrator] No repositories configured. Nothing to do.");
                return;
            }

            _logger.Trace($"[IndexRunOrchestrator] Starting index run for {repos.Count} repositories (IndexVersion={_config.IndexVersion}, Reindex={_config.Reindex}).");

            var repoIndex = 1;
            foreach (var repoId in repos)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await RunForRepositoryAsync(repoId, repoIndex, repos.Count, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.AddException("[IndexRunOrchestrator]", ex, repoId.ToKVP("repoId"));
                }

                repoIndex++;
            }

            _logger.Trace("[IndexRunOrchestrator] Index run completed.");
        }

        private async Task RunForRepositoryAsync(string repoId, int repoIndex, int totalRepos, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_config.Ingestion.SourceRoot)) throw new InvalidOperationException("Ingestion.SourceRoot is not configured.");

            var repoRoot = Path.Combine(_config.Ingestion.SourceRoot, repoId);
            if (!Directory.Exists(repoRoot)) throw new DirectoryNotFoundException($"Repository root not found: {repoRoot}");

            _logger.Trace($"[IndexRunOrchestrator] Repository {repoIndex} of {totalRepos}: '{repoId}' at '{repoRoot}'.");

            if (!GitRepoInspector.TryGetRepoInfo(repoRoot, out RepoInfo repoInfo, out string error)) throw new InvalidOperationException($"Could not get Git repo info for '{repoRoot}': {error}");

            var indexPath = LocalIndexStore.GetDefaultIndexPath(repoRoot);
            var localIndex = LocalIndexStore.Load(indexPath);

            var include = _config.Ingestion.Include ?? new List<string>();
            var exclude = _config.Ingestion.Exclude ?? new List<string>();

            var allFiles = FileWalker.EnumerateFiles(repoRoot, include, exclude).ToList();
            _logger.Trace($"[IndexRunOrchestrator] Repository '{repoId}': discovered {allFiles.Count} candidate files.");

            // Track current file keys (repo-relative, forward slashes) for missing-file detection.
            var currentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fullPath in allFiles)
            {
                var key = ToRepoRelativePath(repoRoot, fullPath);
                currentKeys.Add(key);
            }

            // 1) Handle missing files per IDX-0035.
            var missingRecords = localIndex.GetMissingFiles(currentKeys);
            foreach (var missing in missingRecords.ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.Trace($"[IndexRunOrchestrator] Repository '{repoId}': file missing, scheduling deletion. FilePath='{missing.FilePath}'.");

                var missingContext = new MissingFileContext
                {
                    Config = _config,
                    AgentContext = _agentContext,
                    RepoId = repoId,
                    RepoRoot = repoRoot,
                    RepoInfo = repoInfo,
                    Record = missing
                };

                await _pipeline.HandleMissingFileAsync(missingContext, cancellationToken);

                localIndex.Remove(missing.FilePath);
                localIndex.Save();
            }

            // 2) Compute ActiveContentHash for current files and decide which need indexing.
            int fileIndex = 1;
            int indexedCount = 0;

            foreach (var fullPath in allFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var repoRelative = ToRepoRelativePath(repoRoot, fullPath);
                var localRecord = localIndex.GetOrAdd(repoRelative);

                var activeHash = await ContentHashUtil.ComputeFileContentHashAsync(fullPath, cancellationToken);
                localIndex.UpdateActiveContentHash(repoRelative, activeHash);

                var shouldIndex = ShouldIndexFile(localRecord, activeHash);

                if (!shouldIndex)
                {
                    _logger.Trace($"[IndexRunOrchestrator] Repository '{repoId}': skipping file {fileIndex} of {allFiles.Count} ('{repoRelative}'): no changes and no reindex directive.");
                    fileIndex++;
                    continue;
                }

                _logger.Trace($"[IndexRunOrchestrator] Repository '{repoId}': indexing file {fileIndex} of {allFiles.Count} ('{repoRelative}').");

                var context = new IndexFileContext
                {
                    Config = _config,
                    AgentContext = _agentContext,
                    RepoId = repoId,
                    RepoRoot = repoRoot,
                    RepoInfo = repoInfo,
                    FullPath = fullPath,
                    RepoRelativePath = repoRelative,
                    LocalRecord = localRecord
                };

                await _pipeline.IndexFileAsync(context, cancellationToken);

                // Persist both content hash and facet metadata snapshot to local index.
                localIndex.MarkIndexed(repoRelative, activeHash, DateTime.UtcNow, context.SubKindAfterIndexing, context.Facets);
                localIndex.Save();

                indexedCount++;
                fileIndex++;
            }

            _logger.Trace($"[IndexRunOrchestrator] Repository '{repoId}': index run complete. Indexed {indexedCount} of {allFiles.Count} files.");
        }

        /// <summary>
        /// Decide whether a file should be (re)indexed based on local index state and global config.
        ///
        /// Implements the spirit of IDX-0030 and IDX-0036:
        /// - If the file is new (no ContentHash), index.
        /// - If ActiveContentHash != ContentHash, index.
        /// - If Reindex == "chunk" or "full", index.
        /// - If global config.Reindex == true, index.
        /// </summary>
        private bool ShouldIndexFile(LocalIndexRecord record, string activeHash)
        {
            if (record == null) return true;

            // New file or never successfully indexed.
            if (string.IsNullOrWhiteSpace(record.ContentHash)) return true;

            // Content has changed on disk.
            if (!string.Equals(record.ContentHash, activeHash, StringComparison.OrdinalIgnoreCase)) return true;

            // Manual override.
            if (!string.IsNullOrWhiteSpace(record.Reindex)) return true;

            // Global flag (e.g., forced reindex from config).
            if (_config.Reindex == "true") return true;

            return false;
        }

        private static string ToRepoRelativePath(string repoRoot, string fullPath)
        {
            var rel = Path.GetRelativePath(repoRoot, fullPath);
            // Normalise to forward slashes to keep LocalIndex keys stable.
            return rel.Replace('\\', '/');
        }
    }

    /// <summary>
    /// Context passed to the indexing pipeline for a single file.
    /// The pipeline is responsible for chunking, embedding, vector DB writes,
    /// content-repo persistence, and facet collection.
    /// </summary>
    public class IndexFileContext
    {
        public IngestionConfig Config { get; set; }
        public AgentContext AgentContext { get; set; }
        public string RepoId { get; set; }
        public string RepoRoot { get; set; }
        public RepoInfo RepoInfo { get; set; }
        public string FullPath { get; set; }
        public string RepoRelativePath { get; set; }
        public LocalIndexRecord LocalRecord { get; set; }

        /// <summary>
        /// Optional SubKind value determined during indexing (e.g. Manager, Repository, Model).
        /// If set, the orchestrator will persist it to the local index when marking the file as indexed.
        /// </summary>
        public string SubKindAfterIndexing { get; set; }

        /// <summary>
        /// Facet snapshot for this file, to be written into local-index.json for crash-safe
        /// MetadataRegistry reconstruction.
        /// The indexing pipeline is responsible for populating this collection.
        /// </summary>
        public List<FacetValue> Facets { get; set; } = new List<FacetValue>();
    }

    /// <summary>
    /// Context for handling a missing file (present in local-index.json but not on disk).
    /// The pipeline should delete all vectors for the associated DocId/Path per IDX-0035.
    /// </summary>
    public class MissingFileContext
    {
        public IngestionConfig Config { get; set; }
        public AgentContext AgentContext { get; set; }
        public string RepoId { get; set; }
        public string RepoRoot { get; set; }
        public RepoInfo RepoInfo { get; set; }
        public LocalIndexRecord Record { get; set; }
    }

    /// <summary>
    /// Abstraction over the concrete indexing pipeline (chunking, embeddings, Qdrant, content repo).
    ///
    /// This allows the orchestrator to live in a reusable library while individual
    /// apps/tests provide their own pipeline implementations.
    /// </summary>
    public interface IIndexingPipeline
    {
        Task IndexFileAsync(IndexFileContext context, CancellationToken cancellationToken);

        Task HandleMissingFileAsync(MissingFileContext context, CancellationToken cancellationToken);
    }
}
