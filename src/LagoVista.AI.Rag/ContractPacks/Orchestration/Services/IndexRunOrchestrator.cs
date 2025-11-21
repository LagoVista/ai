using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Orchestration.Interfaces;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.ContractPacks.Orchestration.Services
{
    /// <summary>
    /// High-level orchestrator for an indexing run.
    ///
    /// Responsibilities:
    ///  - Interpret IngestionConfig (SourceRoot, repos, etc.).
    ///  - Discover files via IFileDiscoveryService.
    ///  - Build a FileIngestionPlan via IFileIngestionPlanner.
    ///  - Load/save LocalIndexStore via ILocalIndexStore.
    ///  - Invoke IIndexingPipeline for FilesToIndex.
    ///  - Handle DocsToDelete (TODO: integrate registry deletion semantics).
    ///
    /// This is the spiritual replacement for the legacy IngestorService but
    /// expressed in terms of the new contract-pack interfaces.
    /// </summary>
    public sealed class IndexRunOrchestrator : IIndexRunOrchestrator
    {
        private readonly IngestionConfig _config;
        private readonly IFileDiscoveryService _discoveryService;
        private readonly IFileIngestionPlanner _ingestionPlanner;
        private readonly ILocalIndexStore _localIndexStore;
        private readonly IIndexingPipeline _pipeline;

        public IndexRunOrchestrator(
            IngestionConfig config,
            IFileDiscoveryService discoveryService,
            IFileIngestionPlanner ingestionPlanner,
            ILocalIndexStore localIndexStore,
            IIndexingPipeline pipeline)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _ingestionPlanner = ingestionPlanner ?? throw new ArgumentNullException(nameof(ingestionPlanner));
            _localIndexStore = localIndexStore ?? throw new ArgumentNullException(nameof(localIndexStore));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }

        public async Task RunAsync(string repoId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(repoId))
                throw new ArgumentNullException(nameof(repoId));

            // Determine repo root from config. For now we assume SourceRoot + repoId.
            var sourceRoot = _config.Ingestion?.SourceRoot ?? string.Empty;
            var repoRoot = string.IsNullOrWhiteSpace(sourceRoot)
                ? repoId
                : Path.Combine(sourceRoot, repoId);

            // Ensure the repo root exists; if not, nothing to do.
            if (!Directory.Exists(repoRoot))
                return;

            // Load local index
            var localIndex = await _localIndexStore.LoadAsync(repoId, token).ConfigureAwait(false);

            // Discover files under this repo
            var discovered = await _discoveryService
                .DiscoverAsync(repoRoot, token)
                .ConfigureAwait(false);

            // Build ingestion plan from discovered files and local index
            var discoveredRelativePaths = discovered
                .Select(df => df.RelativePath)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var plan = await _ingestionPlanner
                .BuildPlanAsync(repoId, discoveredRelativePaths, localIndex, token)
                .ConfigureAwait(false);

            // Index files according to plan
            foreach (var file in plan.FilesToIndex)
            {
                token.ThrowIfCancellationRequested();

                var fullPath = Path.Combine(repoRoot, file.CanonicalPath);
                if (!File.Exists(fullPath))
                    continue;

                var ctx = new IndexFileContext
                {
                    OrgId = _config.OrgId,
                    ProjectId = plan.ProjectId,
                    RepoId = repoId,
                    FullPath = fullPath,
                    RelativePath = file.CanonicalPath,
                    Language = null,
                    Metadata = new Dictionary<string, object>()
                };

                // Compute a basic DocumentIdentity so downstream components
                // have something consistent to work with.
                var identity = new DocumentIdentity
                {
                    OrgId = _config.OrgId,
                    ProjectId = plan.ProjectId,
                    RepoId = repoId,
                    RelativePath = file.CanonicalPath
                };
                identity.ComputeDocId();
                ctx.DocumentIdentity = identity;

                await _pipeline.IndexFileAsync(ctx, token).ConfigureAwait(false);
            }

            // TODO: Handle plan.DocsToDelete by invoking registry / Qdrant deletion.
            // For now this is left as a follow-on refactor once the new pipeline
            // is fully validated.

            // Persist updated local index
            await _localIndexStore.SaveAsync(repoId, localIndex, token).ConfigureAwait(false);
        }
    }
}
