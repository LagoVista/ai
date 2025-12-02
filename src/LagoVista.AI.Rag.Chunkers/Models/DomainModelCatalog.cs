using System;
using System.Collections.Generic;
using System.Linq;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// In-memory catalog of domains and models discovered during the pre-scan.
    /// Built once per repository and passed into later stages (chunking, normalization, etc.).
    /// </summary>
    public sealed class DomainModelCatalog
    {
        public IReadOnlyDictionary<string, DomainSummaryInfo> DomainsByKey { get; }

        public IReadOnlyDictionary<string, DomainSummaryInfo> DomainsByKeyName { get; }

        public IReadOnlyDictionary<string, ModelCatalogEntry> ModelsByQualifiedName { get; }

        public DomainModelCatalog(
            IReadOnlyDictionary<string, DomainSummaryInfo> domainsByKey,
            IReadOnlyDictionary<string, DomainSummaryInfo> domainsByKeyName,
            IReadOnlyDictionary<string, ModelCatalogEntry> modelsByQualifiedName)
        {
            DomainsByKey = domainsByKey ?? throw new ArgumentNullException(nameof(domainsByKey));
            DomainsByKeyName = domainsByKeyName ?? throw new ArgumentNullException(nameof(domainsByKeyName));
            ModelsByQualifiedName = modelsByQualifiedName ?? throw new ArgumentNullException(nameof(modelsByQualifiedName));
        }

        public InvokeResult<DomainSummaryInfo> GetDomainByKey(string domainKey)
        {
            if (domainKey == null) throw new ArgumentNullException(nameof(domainKey));

            if(DomainsByKey.ContainsKey(domainKey))
            {
                return InvokeResult<DomainSummaryInfo>.Create(DomainsByKey[domainKey]);
            }
            if(DomainsByKeyName.ContainsKey(domainKey))
            {
                return InvokeResult<DomainSummaryInfo>.Create(DomainsByKeyName[domainKey]);
            }   


            return InvokeResult<DomainSummaryInfo>.FromError($"Domain with key '{domainKey}' not found in catalog.");
        }

        public InvokeResult<ModelCatalogEntry> GetModelByName(string qualifiedName)
        {
            if (qualifiedName == null) throw new ArgumentNullException(nameof(qualifiedName));


            if( ModelsByQualifiedName.TryGetValue(qualifiedName, out ModelCatalogEntry model))
            {
                return InvokeResult<ModelCatalogEntry>.Create(model);
            }

            var fullKey = ModelsByQualifiedName.Keys.FirstOrDefault(key => key.EndsWith(qualifiedName));
            if(fullKey != default)
            {
                return InvokeResult<ModelCatalogEntry>.Create(ModelsByQualifiedName[fullKey]);
            }
           
            return InvokeResult<ModelCatalogEntry>.FromError($"Model with qualified name '{qualifiedName}' not found in catalog."); 
        }
    }

    public class DomainModelHeaderInformation
    {
        public string DomainName { get; set; }
        public string DomainKey { get; set; }
        public string DomainTagLine { get; set; }
        public string ModelName { get; set; }
        public string ModelClassName { get; set; }
        public string ModelTagLine { get; set; }
    }

    /// <summary>
    /// Catalog entry for a single model: location, SubKind, and structured description.
    /// </summary>
    public sealed class ModelCatalogEntry
    {
        public string RepoId { get; set; }
        public string RelativePath { get; set; }
        public SubtypeKind SubKind { get; set; }
        public ModelStructureDescription Structure { get; set; }
    }
}
