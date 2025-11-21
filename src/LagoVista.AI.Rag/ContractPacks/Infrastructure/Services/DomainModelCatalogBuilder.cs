using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;

namespace LagoVista.AI.Rag.ContractPacks.Infrastructure.Services
{
    /// <summary>
    /// Default implementation of <see cref="IDomainModelCatalogBuilder"/>.
    /// Uses SubKind detection plus chunker services to build an in-memory catalog
    /// of domains and models from discovered C# files.
    /// </summary>
    public class DomainModelCatalogBuilder : IDomainModelCatalogBuilder
    {
        private readonly IChunkerServices _chunkerServices;

        public DomainModelCatalogBuilder(IChunkerServices chunkerServices)
        {
            _chunkerServices = chunkerServices ?? throw new ArgumentNullException(nameof(chunkerServices));
        }

        public async Task<DomainModelCatalog> BuildAsync(
            string repoId,
            IReadOnlyList<DiscoveredFile> files,
            CancellationToken token = default)
        {
            if (repoId == null) throw new ArgumentNullException(nameof(repoId));
            if (files == null) throw new ArgumentNullException(nameof(files));

            var domainsByKey = new Dictionary<string, DomainSummaryInfo>(StringComparer.OrdinalIgnoreCase);
            var modelsByQualifiedName = new Dictionary<string, ModelCatalogEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
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

                var result = _chunkerServices.DetectForFile(source, file.RelativePath);

                var snippet = string.IsNullOrWhiteSpace(result.SymbolText)
                    ? source
                    : result.SymbolText;

                // 1) Domains: ExtractDomains will return empty for non-domain snippets.
                var domainInfos = _chunkerServices.ExtractDomains(snippet, file.RelativePath);
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
                    }
                }

                // 2) Models: BuildStructuredDescriptionForModel returns null for non-model snippets.
                var modelStructure = _chunkerServices.BuildStructuredDescriptionForModel(
                    snippet,
                    file.RelativePath,
                    resources: new Dictionary<string, string>());

                if (modelStructure != null && !string.IsNullOrWhiteSpace(modelStructure.QualifiedName))
                {
                    modelsByQualifiedName[modelStructure.QualifiedName] = new ModelCatalogEntry
                    {
                        RepoId = repoId,
                        RelativePath = file.RelativePath,
                        SubKind = result.SubKind,
                        Structure = modelStructure
                    };
                }
            }

            return new DomainModelCatalog(domainsByKey, modelsByQualifiedName);
        }
    }
}
