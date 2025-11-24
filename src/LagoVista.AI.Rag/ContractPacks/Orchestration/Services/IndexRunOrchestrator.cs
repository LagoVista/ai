using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.ContractPacks.Infrastructure.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.ContractPacks.Orchestration.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Registry.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Rag.ContractPacks.Orchestration.Services
{
    /// <summary>
    /// Skeleton implementation of the indexing run orchestrator.
    ///
    /// This class wires together the major contract packs:
    ///  - Configuration (IIngestionConfigProvider)
    ///  - File discovery (IFileDiscoveryService)
    ///  - Local index (ILocalIndexStore)
    ///  - Ingestion planning (IFileIngestionPlanner)
    ///  - Indexing pipeline (IIndexingPipeline)
    ///  - Facet accumulation & registry (IFacetAccumulator, IMetadataRegistryClient)
    ///
    /// Detailed behavior (chunking, embedding, Qdrant, etc.) is delegated to
    /// the underlying services.
    /// </summary>
    public sealed class IndexRunOrchestrator : IIndexRunOrchestrator
    {
        private readonly IIngestionConfigProvider _configProvider;
        private readonly IFileDiscoveryService _discoveryService;
        private readonly IFileIngestionPlanner _ingestionPlanner;
        private readonly ILocalIndexStore _localIndexStore;
        private readonly IIndexingPipeline _pipeline;
        private readonly IFacetAccumulator _facetAccumulator;
        private readonly IMetadataRegistryClient _metadataRegistryClient;
        private readonly IAdminLogger _adminLogger;
        private readonly IGitRepoInspector _gitRepoInspector;

        public IndexRunOrchestrator(
            IIngestionConfigProvider configProvider,
            IAdminLogger adminLogger,
            IFileDiscoveryService discoveryService,
            IFileIngestionPlanner ingestionPlanner,
            ILocalIndexStore localIndexStore,
            IIndexingPipeline pipeline,
            IGitRepoInspector gitRepoInspector,
            IFacetAccumulator facetAccumulator,
            IMetadataRegistryClient metadataRegistryClient)
        {
            _gitRepoInspector = gitRepoInspector ?? throw new ArgumentNullException(nameof(gitRepoInspector));
            _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _ingestionPlanner = ingestionPlanner ?? throw new ArgumentNullException(nameof(ingestionPlanner));
            _localIndexStore = localIndexStore ?? throw new ArgumentNullException(nameof(localIndexStore));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _facetAccumulator = facetAccumulator ?? throw new ArgumentNullException(nameof(facetAccumulator));
            _metadataRegistryClient = metadataRegistryClient ?? throw new ArgumentNullException(nameof(metadataRegistryClient));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task RunAsync(IngestionConfig config, string mode = null, string processRepo = null, bool verbose = false, bool dryrun = false, CancellationToken cancellationToken = default)
        {
            // 1. Load configuration
            if (config == null)
                throw new InvalidOperationException("IIngestionConfigProvider returned null config.");

            var repos = config.Ingestion?.Repositories ?? new List<string>();

            if (!string.IsNullOrEmpty(processRepo))
                repos = repos.Where(rp => rp == processRepo).ToList();

            int idx = 1;

            var totalFilesFound = 0;
            foreach (var repoId in repos)
            {
                cancellationToken.ThrowIfCancellationRequested();

              
                // 2. Resolve repo root
                var sourceRoot = config.Ingestion?.SourceRoot ?? string.Empty;
                var repoRoot = string.IsNullOrWhiteSpace(sourceRoot)
                    ? repoId
                    : Path.Combine(sourceRoot, repoId);

                var gitInfo = _gitRepoInspector.GetRepoInfo(repoRoot);
                if (!gitInfo.Successful)
                {
                    _adminLogger.AddError($"[IndexRunOrchestrator_RunAsync]", $"Git - {gitInfo.ErrorMessage}");
                }
                _adminLogger.Trace($"[IndexRunOrchestrator_RunAsync] - Git Info {gitInfo.Result}");


                _adminLogger.Trace($"[IndexRunOrchestrator_RunAsync] - Starting repo {repoId}; in folder {repoRoot} - {idx} of {repos.Count}.");
                var localIndex = await _localIndexStore.LoadAsync(config, repoId, cancellationToken);

                var discoveredFiles = await _discoveryService.DiscoverAsync(config, repoId, cancellationToken);
                totalFilesFound += discoveredFiles.Count;
                _adminLogger.Trace($"[IndexRunOrchestrator_RunAsync] - Found {discoveredFiles.Count} files.");
                if (verbose)
                {
                    foreach( var file in discoveredFiles)
                    {
                        _adminLogger.Trace($"    Discovered: {file.RelativePath} (Size: {file.SizeBytes} bytes; IsBinary: {file.IsBinary})");
                    }
                }

                idx++;


                var relativePaths = new List<string>();
                foreach (var file in discoveredFiles)
                {
                    if (!string.IsNullOrWhiteSpace(file.RelativePath))
                    {
                        relativePaths.Add(file.RelativePath);
                    }
                }

                var plan = await _ingestionPlanner
                    .BuildPlanAsync(repoId, relativePaths, localIndex, cancellationToken)
                    .ConfigureAwait(false);

                _adminLogger.Trace($"[IndexRunOrchestrator_RunAsync] Found {plan.FilesToIndex.Count} files to index.");

                foreach(var filePlan in plan.FilesToIndex)
                {
                    var fullPath = Path.Combine(repoRoot, filePlan.CanonicalPath ?? string.Empty);
                    if (!File.Exists(fullPath))
                        continue;

                    var ctx = new IndexFileContext
                    {
                        OrgId = config.OrgId,
                        ProjectId = plan.ProjectId,
                        RepoId = repoId,
                        FullPath = fullPath,
                        RelativePath = filePlan.CanonicalPath,
                        Metadata = new System.Collections.Generic.Dictionary<string, object>()
                    };
                }


                //    if (!Directory.Exists(repoRoot))
                //    {
                //        // No local repo folder; skip for now.
                //        continue;
                //    }

                //    // 3. Load local index for this repo
                //    var localIndex = await _localIndexStore.LoadAsync(repoId, cancellationToken).ConfigureAwait(false);

                //    // 4. Discover files
                //    var discovered = await _discoveryService
                //        .DiscoverAsync(repoRoot, cancellationToken)
                //        .ConfigureAwait(false);

                //    var relativePaths = new List<string>();
                //    foreach (var file in discovered)
                //    {
                //        if (!string.IsNullOrWhiteSpace(file.RelativePath))
                //        {
                //            relativePaths.Add(file.RelativePath);
                //        }
                //    }

                //    // 5. Build ingestion plan
                //    var plan = await _ingestionPlanner
                //        .BuildPlanAsync(repoId, relativePaths, localIndex, cancellationToken)
                //        .ConfigureAwait(false);

                //    // 6. Index files according to plan
                //    foreach (var filePlan in plan.FilesToIndex)
                //    {
                //        cancellationToken.ThrowIfCancellationRequested();

                //        var fullPath = Path.Combine(repoRoot, filePlan.CanonicalPath ?? string.Empty);
                //        if (!File.Exists(fullPath))
                //            continue;

                //        var ctx = new IndexFileContext
                //        {
                //            OrgId = config.OrgId,
                //            ProjectId = plan.ProjectId,
                //            RepoId = repoId,
                //            FullPath = fullPath,
                //            RelativePath = filePlan.CanonicalPath,
                //            Metadata = new System.Collections.Generic.Dictionary<string, object>()
                //        };

                //        await _pipeline.IndexFileAsync(ctx, cancellationToken).ConfigureAwait(false);

                //        // TODO: update localIndex for this file (hash, facets, LastIndexedUtc)
                //        // and immediately persist via _localIndexStore.SaveAsync to be crash-safe.
                //        // TODO: accumulate facets via _facetAccumulator as the pipeline begins
                //        // returning them (once that contract is in place).
                //    }

                //    // TODO: handle plan.DocsToDelete by calling IQdrantVectorStore (via pipeline or
                //    // a dedicated deletion path) and updating localIndex accordingly.

                //    // Finally, persist local index for this repo.
                //    await _localIndexStore.SaveAsync(repoId, localIndex, cancellationToken).ConfigureAwait(false);
                //}

                //// After all repos, flush accumulated facets to metadata registry.
                //var allFacets = _facetAccumulator.GetAll();
                //if (allFacets.Count > 0)
                //{
                //    // Note: ProjectId / RepoId here may need to be expanded to a per-repo
                //    // call in future. For now, we use the last config.ProjectId and leave
                //    // the aggregation semantics to the implementation.
                //    await _metadataRegistryClient
                //        .ReportFacetsAsync(
                //            orgId: null,            // TODO: supply org id when facet aggregation is wired
                //            projectId: null,        // TODO: supply project id
                //            repoId: null,           // TODO: supply repo id or support multi-repo aggregation
                //            facets: allFacets,
                //            cancellationToken: cancellationToken)
                //        .ConfigureAwait(false);
            }

            _adminLogger.Trace($"[IndexRunOrchestrator_RunAsync] - Total Files Found {totalFilesFound} files.");
        }
    }
}
