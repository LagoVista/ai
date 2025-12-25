using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.ContractPacks.Content.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Infrastructure.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Services;
using LagoVista.AI.Rag.ContractPacks.Orchestration.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Registry.Interfaces;
using LagoVista.AI.Rag.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Rag.Services;
using LagoVista.Core.Interfaces;
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
        private readonly IFileDiscoveryService _discoveryService;
        private readonly IFileIngestionPlanner _ingestionPlanner;
        private readonly ILocalIndexStore _localIndexStore;
        private readonly IIndexingPipeline _pipeline;
        private readonly IIndexFileContextBuilder _indexFileContextBuilder;
        private readonly IFacetAccumulator _facetAccumulator;
        private readonly IMetadataRegistryClient _metadataRegistryClient;
        private readonly IAdminLogger _adminLogger;
        private readonly IGitRepoInspector _gitRepoInspector;
        private readonly ISourceFileProcessor _sourceFileProcessor;
        private readonly IDomainModelCatalogBuilder _domainModelCatalogBuilder;
        private readonly IResxLabelScanner _resxLabelScanner;
        private readonly IEmbedder _embedder;
        private readonly IQdrantClient _qdrantClient;
        private readonly IContentStorage _contentStorage;
        private readonly ITitleDescriptionRefinementOrchestrator _titleDescriptionRefinementOrchestrator;
        private readonly IDomainCatalogService _domainCatalogService;

        public IndexRunOrchestrator(
            IIngestionConfigProvider configProvider,
            IAdminLogger adminLogger,
            ISourceFileProcessor sourceFileProcessor,
            IFileDiscoveryService discoveryService,
            IFileIngestionPlanner ingestionPlanner,
            ILocalIndexStore localIndexStore,
            IIndexingPipeline pipeline,
            IGitRepoInspector gitRepoInspector,
            IFacetAccumulator facetAccumulator,
            IDomainModelCatalogBuilder domainModelCatalogBuilder,
            IIndexFileContextBuilder indexFileContextBuilder,
            IResxLabelScanner resxLabelScanner,
            IEmbedder embedder,
            IContentStorage contentStorage,
            IQdrantClient qdrantClient,
            IDomainCatalogService domainCatalogService,
            ITitleDescriptionRefinementOrchestrator titleDescriptionRefinementOrchestrator,
            IMetadataRegistryClient metadataRegistryClient)
        {
            _indexFileContextBuilder = indexFileContextBuilder ?? throw new ArgumentNullException(nameof(indexFileContextBuilder));
            _gitRepoInspector = gitRepoInspector ?? throw new ArgumentNullException(nameof(gitRepoInspector));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _ingestionPlanner = ingestionPlanner ?? throw new ArgumentNullException(nameof(ingestionPlanner));
            _localIndexStore = localIndexStore ?? throw new ArgumentNullException(nameof(localIndexStore));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _facetAccumulator = facetAccumulator ?? throw new ArgumentNullException(nameof(facetAccumulator));
            _metadataRegistryClient = metadataRegistryClient ?? throw new ArgumentNullException(nameof(metadataRegistryClient));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _sourceFileProcessor = sourceFileProcessor ?? throw new ArgumentNullException(nameof(sourceFileProcessor));
            _domainModelCatalogBuilder = domainModelCatalogBuilder ?? throw new ArgumentNullException(nameof(domainModelCatalogBuilder));
            _resxLabelScanner = resxLabelScanner ?? throw new ArgumentNullException(nameof(resxLabelScanner));
            _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
            _qdrantClient = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
            _contentStorage = contentStorage ?? throw new ArgumentNullException(nameof(contentStorage));
            _domainCatalogService = domainCatalogService ?? throw new ArgumentNullException(nameof(domainCatalogService));
            _titleDescriptionRefinementOrchestrator = titleDescriptionRefinementOrchestrator ?? throw new ArgumentNullException(nameof(titleDescriptionRefinementOrchestrator));
        }

        public async Task RunAsync(IngestionConfig config, string mode = null, string processRepo = null, SubtypeKind? subKindFilter = null, bool verbose = false, bool dryrun = false, CancellationToken cancellationToken = default)
        {
            // 1. Load configuration
            if (config == null)
                throw new InvalidOperationException("IIngestionConfigProvider returned null config.");

            var repos = config.Ingestion?.Repositories ?? new List<string>();

            if (!string.IsNullOrEmpty(processRepo))
                repos = repos.Where(rp => rp.ToLower() == processRepo.ToLower()).ToList();

            int idx = 1;

            await _qdrantClient.EnsureCollectionAsync(config.Qdrant.Collection);

            var sw = Stopwatch.StartNew();
            _adminLogger.Trace("[IndexRunOrchestrator_RunAsync] - Finding all CSharp Files - this could take a moment.");
            var allDiscoveredFiles = await _discoveryService.DiscoverAsync(config, "*.cs", cancellationToken);
            _adminLogger.Trace($"[IndexRunOrchestrator_RunAsync] - Found {allDiscoveredFiles.Count} files in {sw.Elapsed.TotalMilliseconds}ms.");

            sw.Restart();
            _adminLogger.Trace("[IndexRunOrchestrator_RunAsync] - Finding all Resources - this could take a moment.");
            var resourceFiles = _resxLabelScanner.ScanResxTree(config.Ingestion.SourceRoot);
            var resources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var parentDictionary in resourceFiles)
            {
                foreach (var dictionary in parentDictionary.Value)
                {
                    if (!resources.ContainsKey(dictionary.Key))
                    {
                        resources[dictionary.Key] = dictionary.Value;
                    }
                }
            }

            var resourceDictionary = new ResourceDictionary(resources);

            _adminLogger.Trace($"[IndexRunOrchestrator_RunAsync] - Found {resources.Count} in  all Resources {sw.Elapsed.TotalMilliseconds}ms.");
            if (mode == "refine")
            {
                //var fullDomainCatalog = await _domainModelCatalogBuilder.BuildAsync(allDiscoveredFiles, resources);
                await _titleDescriptionRefinementOrchestrator.RunAsync(allDiscoveredFiles, resourceFiles, cancellationToken);
            }
            else if(mode == "domaincatalog")
            {
                await _domainCatalogService.BuildCatalogAsync(allDiscoveredFiles, resources, cancellationToken);
            }
            else
            {
                var totalFilesFound = 0;
                var totalPartsToIndex = 0;
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
                    var discoveredFiles = await _discoveryService.DiscoverAsync(config, repoId, null, cancellationToken);
                    totalFilesFound += discoveredFiles.Count;
                    _adminLogger.Trace($"[IndexRunOrchestrator_RunAsync] - Found {discoveredFiles.Count} files.");
                    if (verbose)
                    {
                        foreach (var file in discoveredFiles)
                        {
                            _adminLogger.Trace($"    Discovered: {file.RelativePath} (Size: {file.SizeBytes} bytes; IsBinary: {file.IsBinary})");
                        }
                    }

                    var relativePaths = new List<string>();
                    foreach (var file in discoveredFiles)
                    {
                        if (!string.IsNullOrWhiteSpace(file.RelativePath))
                        {
                            relativePaths.Add(file.RelativePath);
                        }
                    }


                    var domainCatalog = await _domainModelCatalogBuilder.BuildAsync(repoId, discoveredFiles, resources);

                    var plan = await _ingestionPlanner.BuildPlanAsync(repoId, relativePaths, localIndex, cancellationToken);

                    _adminLogger.Trace($"[IndexRunOrchestrator_RunAsync] Found {plan.FilesToIndex.Count} files to index.");

                    foreach (var filePlan in plan.FilesToIndex)
                    {
                        var fullPath = Path.Combine(repoRoot, filePlan.CanonicalPath ?? string.Empty);
                        if (!File.Exists(fullPath))
                            continue;

                        var fileContext = await _indexFileContextBuilder.BuildAsync(config, gitInfo.Result, repoId, filePlan, localIndex, cancellationToken);

                        var fileProcessResult = await _sourceFileProcessor.BuildChunks(config, fileContext, domainCatalog, subKindFilter, resources);
                        if (fileProcessResult.Successful)
                        {
                            if (fileProcessResult.Result.RagPoints.Count == 0)
                            {
                                Console.WriteLine($"No points: {fileContext.RelativePath}");
                            }

                            foreach (var result in fileProcessResult.Result.RagPoints)
                            {
                                totalPartsToIndex++;
                                if (String.IsNullOrEmpty(result.Payload.DocId))
                                    throw new ArgumentNullException("DocId");

                                _adminLogger.Trace($"[IndexRunOrchestrator__RunAsync] {result.Payload.SemanticId}");

                                var embedResult = await _embedder.EmbedAsync(System.Text.UTF8Encoding.UTF8.GetString(result.Contents));
                                result.Vector = embedResult.Result.Vector;
                                result.Payload.EmbeddingModel = embedResult.Result.EmbeddingModel;

                                await _contentStorage.AddContentAsync(result.Payload.SourceSliceBlobUri, result.Contents);
                            }

                            await _qdrantClient.UpsertInBatchesAsync(config.Qdrant.Collection, fileProcessResult.Result.RagPoints, config.Qdrant.VectorSize);

                            var record = localIndex.GetOrAdd(fileContext.RelativePath, fileContext.DocumentIdentity.DocId);
                            record.ContentHash = await ContentHashUtil.ComputeFileContentHashAsync(fileContext.FullPath);
                            await _localIndexStore.SaveAsync(config, repoId, localIndex, cancellationToken);

                            await _contentStorage.AddContentAsync(fileContext.FullPath, fileContext.Contents);

                            Console.WriteLine(new String('-', 80));
                        }
                        else
                        {
                            _adminLogger.AddError($"[IndexRunOrchestrator_RunAsync]", $"{fileProcessResult.ErrorMessage} - {fullPath}");
                        }
                    }


                    idx++;
                }

                // After all repos, flush accumulated facets to metadata registry.
                var allFacets = _facetAccumulator.GetAll();
                if (allFacets.Count > 0)
                {
                    // Note: ProjectId / RepoId here may need to be expanded to a per-repo
                    // call in future. For now, we use the last config.ProjectId and leave
                    // the aggregation semantics to the implementation.
                    await _metadataRegistryClient
                        .ReportFacetsAsync(
                            orgId: null,            // TODO: supply org id when facet aggregation is wired
                            projectId: null,        // TODO: supply project id
                            repoId: null,           // TODO: supply repo id or support multi-repo aggregation
                            facets: allFacets,
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }


                _adminLogger.Trace($"[IndexRunOrchestrator_RunAsync] - Total Files Found {totalFilesFound} files, created {totalPartsToIndex} indexes.");
            }
        }
    }
}
