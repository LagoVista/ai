using System;
using System.Collections.Generic;
using LagoVista.AI.Rag.Chunkers.Services;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// In-memory catalog of domains and models discovered during the pre-scan.
    /// Built once per repository and passed into later stages (chunking, normalization, etc.).
    /// </summary>
    public sealed class DomainModelCatalog
    {
        public IReadOnlyDictionary<string, DomainSummaryInfo> DomainsByKey { get; }

        public IReadOnlyDictionary<string, ModelCatalogEntry> ModelsByQualifiedName { get; }

        public DomainModelCatalog(
            IReadOnlyDictionary<string, DomainSummaryInfo> domainsByKey,
            IReadOnlyDictionary<string, ModelCatalogEntry> modelsByQualifiedName)
        {
            DomainsByKey = domainsByKey ?? throw new ArgumentNullException(nameof(domainsByKey));
            ModelsByQualifiedName = modelsByQualifiedName ?? throw new ArgumentNullException(nameof(modelsByQualifiedName));
        }

        public bool TryGetDomain(string domainKey, out DomainSummaryInfo domain)
        {
            if (domainKey == null) throw new ArgumentNullException(nameof(domainKey));
            return DomainsByKey.TryGetValue(domainKey, out domain);
        }

        public bool TryGetModel(string qualifiedName, out ModelCatalogEntry model)
        {
            if (qualifiedName == null) throw new ArgumentNullException(nameof(qualifiedName));
            return ModelsByQualifiedName.TryGetValue(qualifiedName, out model);
        }
    }

    /// <summary>
    /// Catalog entry for a single model: location, SubKind, and structured description.
    /// </summary>
    public sealed class ModelCatalogEntry
    {
        public string RepoId { get; set; }
        public string RelativePath { get; set; }
        public CodeSubKind SubKind { get; set; }
        public ModelStructureDescription Structure { get; set; }
    }
}
