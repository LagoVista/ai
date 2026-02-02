using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.FileServices.Indexes;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Chunkers
{
    public class IndexRunner : IIndexRunner
    {
        private readonly IIndexingPipeline _pipeline;
        private readonly IFileDiscoveryService _discoveryService;
        private readonly IResxLabelScanner _resxLabelScanner;
        private readonly IGitRepoInspector _gitRepoInspector;
        private readonly ILocalIndexStore _localIndexStore;
        private readonly IAdminLogger _adminLogger;
        private readonly IDomainModelCatalogBuilder _domainModelCatalogBuilder;
        private readonly IIndexFileContextBuilder _indexFileContextBuilder;
        private readonly IFileIngestionPlanner _ingestionPlanner;
        private readonly bool _verbose = false;

        public IndexRunner(IIndexingPipeline pipeline, IResxLabelScanner resxLabelScanner, IAdminLogger adminLogger, IGitRepoInspector gitRepoInspector, IIndexFileContextBuilder indexFileContextBuilder,
                           IFileIngestionPlanner ingestionPlanner, IDomainModelCatalogBuilder domainModelCatalogBuilder, ILocalIndexStore localIndexStore, IFileDiscoveryService discoveryService)
        {
            _pipeline = pipeline;
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _resxLabelScanner = resxLabelScanner ?? throw new ArgumentNullException(nameof(resxLabelScanner));
            _gitRepoInspector = gitRepoInspector ?? throw new ArgumentNullException(nameof(gitRepoInspector));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _domainModelCatalogBuilder = domainModelCatalogBuilder ?? throw new ArgumentNullException(nameof(domainModelCatalogBuilder));
            _ingestionPlanner = ingestionPlanner ?? throw new ArgumentNullException(nameof(ingestionPlanner));
            _localIndexStore = localIndexStore ?? throw new ArgumentNullException(nameof(localIndexStore));
            _indexFileContextBuilder = indexFileContextBuilder ?? throw new ArgumentNullException(nameof(indexFileContextBuilder));
        }

        public async Task RunAsync(IngestionConfig config, string processRepo = null, SubtypeKind? subKindFilter = null, CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            _adminLogger.Trace($"{this.Tag()} - Finding all CSharp Files - this could take a moment.");
            var allDiscoveredFiles = await _discoveryService.DiscoverAsync(config, "*.cs", cancellationToken);
            _adminLogger.Trace($"{this.Tag()} - Found {allDiscoveredFiles.Count} files in {sw.Elapsed.TotalMilliseconds}ms.");

            if (config == null)
                throw new InvalidOperationException("IIngestionConfigProvider returned null config.");

            var repos = config.Ingestion?.Repositories ?? new List<string>();

            if (!string.IsNullOrEmpty(processRepo))
                repos = repos.Where(rp => rp.ToLower() == processRepo.ToLower()).ToList();

            sw.Restart();
            _adminLogger.Trace($"{this.Tag()} - Finding all Resources - this could take a moment.");
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
            _adminLogger.Trace($"{this.Tag()} - Found {resources.Count} in  all Resources {sw.Elapsed.TotalMilliseconds}ms.");

            var resourceDictionary = new ResourceDictionary(resources);
            var totalFilesFound = 0;
            var totalPartsToIndex = 0;
            var idx = 0;
            foreach (var repoId in repos)
            {
                var sourceRoot = config.Ingestion?.SourceRoot ?? string.Empty;
                var repoRoot = string.IsNullOrWhiteSpace(sourceRoot)
                    ? repoId
                    : Path.Combine(sourceRoot, repoId);


                var gitInfo = _gitRepoInspector.GetRepoInfo(repoRoot);
                if (!gitInfo.Successful)
                {
                    _adminLogger.AddError(this.Tag(), $"Git - {gitInfo.ErrorMessage}");
                }
                _adminLogger.Trace($"{this.Tag()} - Git Info {gitInfo.Result}");


                _adminLogger.Trace($"{this.Tag()} - Starting repo {repoId}; in folder {repoRoot} - {idx} of {repos.Count}.");

                var localIndex = await _localIndexStore.LoadAsync(config, repoId, cancellationToken);
                var discoveredFiles = await _discoveryService.DiscoverAsync(config, repoId, null, cancellationToken);
                totalFilesFound += discoveredFiles.Count;
                if (_verbose)
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

                totalFilesFound += discoveredFiles.Count;
                _adminLogger.Trace($"{this.Tag()} - Found {discoveredFiles.Count} files.");

                var domainCatalog = await _domainModelCatalogBuilder.BuildAsync(repoId, discoveredFiles, resources);
                var plan = await _ingestionPlanner.BuildPlanAsync(repoId, relativePaths, localIndex, cancellationToken);
                foreach (var filePlan in plan.FilesToIndex)
                {
                    var fileContext = await _indexFileContextBuilder.BuildAsync(config, gitInfo.Result, repoId, filePlan, localIndex, cancellationToken);
                    await _pipeline.IndexFileAsync(domainCatalog, resources, fileContext);
                }

            }
        }
    }
}
