using LagoVista.AI.Chunkers.Providers.DomainDescription;
using LagoVista.AI.Rag.Chunkers.Models;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Indexing.Models
{
    /// <summary>
    /// Read-only lookup/reference data for indexing. No services should be placed here.
    /// </summary>
    public class IndexingResources
    {
        public IndexingResources(IndexFileContext fileCtx, DomainModelCatalog domainCatalog, Dictionary<string, string> resourceDictionary)
        {
            FileContext = fileCtx ?? throw new ArgumentNullException(nameof(fileCtx));
            ResourceDictionary  = resourceDictionary ?? throw new ArgumentNullException(nameof(resourceDictionary));
            DomainCatalog = domainCatalog ?? throw new ArgumentNullException(nameof(domainCatalog));
        }

        public Dictionary<string, string> ResourceDictionary { get; }
        public DomainModelCatalog DomainCatalog { get; }

        public IndexFileContext FileContext { get; }

    }
}
