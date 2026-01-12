using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Chunkers.Providers.DomainDescription;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Indexing.Services
{
    /// <summary>
    /// Default implementation of <see cref="IDomainModelCatalogBuilder"/>.
    /// Uses SubKind detection plus chunker services to build an in-memory catalog
    /// of domains and models from discovered C# files.
    /// </summary>
    public class DomainModelCatalogBuilder : IDomainModelCatalogBuilder
    {
        private readonly IChunkerServices _chunkerServices;
        private readonly IAdminLogger _adminLogger;
        private readonly ICSharpSymbolSplitterService _splitterService;

        public DomainModelCatalogBuilder(IChunkerServices chunkerServices, ICSharpSymbolSplitterService splitterService, IAdminLogger adminLogger)
        {
            _chunkerServices = chunkerServices ?? throw new ArgumentNullException(nameof(chunkerServices));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _splitterService = splitterService ?? throw new ArgumentNullException(nameof(splitterService));
        }

        public Task<DomainModelCatalog> BuildAsync(
           IReadOnlyList<DiscoveredFile> files,
           IReadOnlyDictionary<string, string> resources,
           CancellationToken token = default)
        {
            return BuildAsync(null, files, resources, token);
        }

        public async Task<DomainModelCatalog> BuildAsync(
            string repoId,
            IReadOnlyList<DiscoveredFile> files,
            IReadOnlyDictionary<string, string> resources,
            CancellationToken token = default)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));

            var domainsByKey = new Dictionary<string, DomainSummaryInfo>(StringComparer.OrdinalIgnoreCase);
            var domainsByKeyName = new Dictionary<string, DomainSummaryInfo>(StringComparer.OrdinalIgnoreCase);
            var modelsByQualifiedName = new Dictionary<string, ModelCatalogEntry>(StringComparer.OrdinalIgnoreCase);

            _adminLogger.Trace($"[DomainModelCatalogBuilder__BuildAsync] - will scan {files.Count} for domain or models.");

            var idx = 0;

            foreach (var file in files)
            {
                if (idx % 100 == 0)
                    _adminLogger.Trace($"[DomainModelCatalogBuilder__BuildAsync] - scanned {idx} of {files.Count} files - Found {domainsByKey.Count} domains and {modelsByQualifiedName.Count}, {(idx * 100.0 / files.Count).ToString("0.0")}% complete.");

                if (token.IsCancellationRequested)
                {
                    token.ThrowIfCancellationRequested();
                }



                // Only consider C# files for domain/model catalog.
                if (!file.FullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!File.Exists(file.FullPath))
                    continue;

                var source = await File.ReadAllTextAsync(file.FullPath, token).ConfigureAwait(false);

                var splitterResults = _splitterService.Split(source);
                if (splitterResults.Successful)
                {
                    foreach (var splitrResult in splitterResults.Result)
                    {
                        var result = _chunkerServices.DetectForFile(splitrResult.Text, file.RelativePath);

                        if (result != null)
                        {
                            var snippet = string.IsNullOrWhiteSpace(result.SymbolText)
                                ? source
                                : result.SymbolText;

                            // 1) Domains: ExtractDomains will return empty for non-domain snippets.
                            var domainInfos = _chunkerServices.ExtractDomains(snippet);
                            if (domainInfos != null)
                            {
                                foreach (var domain in domainInfos)
                                {
                                    if (string.IsNullOrWhiteSpace(domain.DomainKey))
                                        continue;

                                    if (!domainsByKey.ContainsKey(domain.DomainKey))
                                    {
                                        domainsByKey[domain.DomainKey] = domain;
                                    }

                                    if (!string.IsNullOrEmpty(domain.DomainKeyName) && !domainsByKeyName.ContainsKey(domain.DomainKeyName))
                                    {
                                        domainsByKeyName[domain.DomainKeyName] = domain;
                                    }
                                }
                            }

                            // TODO: Need to access a provider
                            //// 2) Models: BuildStructuredDescriptionForModel returns null for non-model snippets.
                            //var modelResult = _codeDescriptionService.BuildModelStructureDescription(snippet, resources);

                            //if (modelResult != null && modelResult.Successful && modelResult.Result != null &&
                            //    !string.IsNullOrWhiteSpace(modelResult.Result.QualifiedName))
                            //{
                            //    var modelStructure = modelResult.Result;

                            //    modelsByQualifiedName[modelStructure.QualifiedName] = new ModelCatalogEntry
                            //    {
                            //        RepoId = repoId,
                            //        RelativePath = file.RelativePath,
                            //        SubKind = result.SubKind,
                            //        Structure = modelStructure
                            //    };
                            //}
                        }
                    }
                }

                idx++;
            }

            return new DomainModelCatalog(domainsByKey, domainsByKeyName, modelsByQualifiedName);
        }
    }
}
