using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Models;                 // DomainSummaryInfo
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;    // DiscoveredFile
using LagoVista.AI.Rag.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// IDX-071 implementation: Domain Catalog Service.
    ///
    /// Responsibility:
    /// - Build an immutable DomainCatalog from discovered C# files.
    /// - Persist/load the catalog to/from [SourceRoot]/rag-common/domain-master-catalog.json.
    /// - Provide simple query APIs over the current snapshot.
    ///
    /// NOTE:
    /// - Domain and model extraction logic is implemented in partial class files
    ///   so it can be iterated on independently.
    /// </summary>
    public sealed partial class DomainCatalogService : IDomainCatalogService
    {
        private readonly IAdminLogger _adminLogger;
        private readonly IngestionConfig _ingestionConfig;

        // In-memory immutable snapshot; null until loaded/built.
        private DomainCatalog _catalog;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        public DomainCatalogService(
            IAdminLogger adminLogger,
            IngestionConfig ingestionConfig)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _ingestionConfig = ingestionConfig ?? throw new ArgumentNullException(nameof(ingestionConfig));
        }

        /// <inheritdoc />
        public async Task<InvokeResult> BuildCatalogAsync(
            IReadOnlyList<DiscoveredFile> files,
            IReadOnlyDictionary<string, string> resources,
            CancellationToken cancellationToken = default)
        {
            if (files == null)
            {
                return InvokeResult.FromError("File list for DomainCatalogService.BuildCatalogAsync cannot be null.");
            }

            if (resources == null)
            {
                return InvokeResult.FromError("Resources map for DomainCatalogService.BuildCatalogAsync cannot be null.");
            }

            try
            {
                _adminLogger.Trace($"[DomainCatalogService__BuildCatalogAsync] - starting catalog build for {files.Count} discovered files.");

                var catalog = await BuildCatalogFromFilesAsync(files, resources, cancellationToken).ConfigureAwait(false);

                if (catalog == null)
                {
                    return InvokeResult.FromError("DomainCatalogService.BuildCatalogAsync produced a null catalog.");
                }

                // Replace in-memory snapshot.
                _catalog = catalog;

                var saveResult = await SaveCatalogAsync(cancellationToken).ConfigureAwait(false);
                if (!saveResult.Successful)
                {
                    return saveResult;
                }

                _adminLogger.Trace("[DomainCatalogService__BuildCatalogAsync] - catalog build completed successfully.");

                return InvokeResult.Success;
            }
            catch (OperationCanceledException)
            {
                _adminLogger.AddCustomEvent(Core.PlatformSupport.LogLevel.Warning, "[DomainCatalogService__BuildCatalogAsync]", "[DomainCatalogService__BuildCatalogAsync] - operation canceled.");
                throw;
            }
            catch (Exception ex)
            {
                _adminLogger.AddException("[DomainCatalogService__BuildCatalogAsync] - unhandled exception.", ex);
                return InvokeResult.FromException("DomainCatalogService.BuildCatalogAsync failed.", ex);
            }
        }

        /// <inheritdoc />
        public async Task<InvokeResult> LoadCatalogAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var path = GetCatalogPath();

                if (!System.IO.File.Exists(path))
                {
                    return InvokeResult.FromError($"Domain catalog file not found at '{path}'.");
                }

                _adminLogger.Trace($"[DomainCatalogService__LoadCatalogAsync] - loading catalog from '{path}'.");

                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new StreamReader(stream))
                {
                    var json = await reader.ReadToEndAsync().ConfigureAwait(false);
                    var catalog = JsonConvert.DeserializeObject<DomainCatalog>(json, JsonSettings);

                    if (catalog == null)
                    {
                        return InvokeResult.FromError("Failed to deserialize domain catalog.");
                    }

                    _catalog = catalog;
                }

                return InvokeResult.Success;
            }
            catch (Exception ex)
            {
                _adminLogger.AddException("[DomainCatalogService__LoadCatalogAsync] - unhandled exception.", ex);
                return InvokeResult.FromException("DomainCatalogService.LoadCatalogAsync failed.", ex);
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<DomainEntry> GetAllDomains()
        {
            return _catalog?.Domains ?? Array.Empty<DomainEntry>();
        }

        /// <inheritdoc />
        public IReadOnlyList<ModelClassEntry> GetClassesForDomain(string domainKey)
        {
            if (string.IsNullOrWhiteSpace(domainKey) || _catalog == null)
            {
                return Array.Empty<ModelClassEntry>();
            }

            var comparer = StringComparer.OrdinalIgnoreCase;

            var domain = _catalog.Domains.FirstOrDefault(d => comparer.Equals(d.DomainKey, domainKey));
            if (domain == null)
            {
                return Array.Empty<ModelClassEntry>();
            }

            return domain.Classes ?? Array.Empty<ModelClassEntry>();
        }

        /// <inheritdoc />
        public InvokeResult<DomainEntry> FindDomainForClass(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                return InvokeResult<DomainEntry>.FromError("Class name must be provided.");
            }

            if (_catalog == null)
            {
                return InvokeResult<DomainEntry>.FromError("Domain catalog has not been loaded or built.");
            }

            try
            {
                var comparer = StringComparer.OrdinalIgnoreCase;

                // Match by either simple or fully-qualified name.
                var matchingClass = _catalog.Classes.FirstOrDefault(c =>
                    comparer.Equals(c.ClassName, className) ||
                    comparer.Equals(c.QualifiedClassName, className));

                if (matchingClass == null)
                {
                    return InvokeResult<DomainEntry>.FromError("Class not found in domain catalog.");
                }

                var domain = _catalog.Domains.FirstOrDefault(d =>
                    comparer.Equals(d.DomainKey, matchingClass.DomainKey));

                if (domain == null)
                {
                    return InvokeResult<DomainEntry>.FromError("Owning domain for class was not found in catalog.");
                }

                return InvokeResult<DomainEntry>.Create(domain);
            }
            catch (Exception ex)
            {
                _adminLogger.AddException("[DomainCatalogService__FindDomainForClass] - unhandled exception.", ex);
                return InvokeResult<DomainEntry>.FromException("DomainCatalogService.FindDomainForClass failed.", ex);
            }
        }

        #region Helpers

        private string GetCatalogPath()
        {
            if (_ingestionConfig?.Ingestion == null || string.IsNullOrWhiteSpace(_ingestionConfig.Ingestion.SourceRoot))
            {
                throw new InvalidOperationException("IngestionConfig.Ingestion.SourceRoot must be configured for DomainCatalogService.");
            }

            var root = _ingestionConfig.Ingestion.SourceRoot;
            var ragCommon = Path.Combine(root, "rag-common");

            if (!Directory.Exists(ragCommon))
            {
                Directory.CreateDirectory(ragCommon);
            }

            return Path.Combine(ragCommon, "domain-master-catalog.json");
        }

        private async Task<InvokeResult> SaveCatalogAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_catalog == null)
                {
                    return InvokeResult.FromError("Cannot save a null domain catalog.");
                }

                var path = GetCatalogPath();

                _adminLogger.Trace($"[DomainCatalogService__SaveCatalogAsync] - saving catalog to '{path}'.");

                var json = JsonConvert.SerializeObject(_catalog, JsonSettings);

                using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    await writer.WriteAsync(json).ConfigureAwait(false);
                }

                return InvokeResult.Success;
            }
            catch (Exception ex)
            {
                _adminLogger.AddException("[DomainCatalogService__SaveCatalogAsync] - unhandled exception.", ex);
                return InvokeResult.FromException("DomainCatalogService.SaveCatalogAsync failed.", ex);
            }
        }

        /// <summary>
        /// Build a DomainCatalog from the provided file list and resources.
        ///
        /// This method delegates domain and model extraction to partial-class
        /// helpers so that discovery logic can evolve independently.
        /// </summary>
        private async Task<DomainCatalog> BuildCatalogFromFilesAsync(
            IReadOnlyList<DiscoveredFile> files,
            IReadOnlyDictionary<string, string> resources,
            CancellationToken cancellationToken)
        {
            _adminLogger.Trace($"[DomainCatalogService__BuildCatalogFromFilesAsync] - scanning {files.Count} files for domains and models.");

            var domainSummaries = await ExtractDomainsAsync(files, cancellationToken).ConfigureAwait(false);
            var modelClasses = await ExtractModelClassesAsync(files, resources, cancellationToken).ConfigureAwait(false);

            // Group model classes by DomainKey so we can attach them to domain entries.
            var classesByDomain = modelClasses
                .GroupBy(mc => mc.DomainKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<ModelClassEntry>)g.ToList(), StringComparer.OrdinalIgnoreCase);

            var domains = new List<DomainEntry>();

            foreach (var summary in domainSummaries)
            {
                if (string.IsNullOrWhiteSpace(summary.DomainKey) ||
                    string.IsNullOrWhiteSpace(summary.Title) ||
                    string.IsNullOrWhiteSpace(summary.Description))
                {
                    throw new InvalidOperationException($"Domain descriptor '{summary.DomainKey ?? "<null>"}' is missing required fields.");
                }

                if (!classesByDomain.TryGetValue(summary.DomainKey, out var classesForDomain))
                {
                    classesForDomain = Array.Empty<ModelClassEntry>();
                }

                var domainEntry = new DomainEntry(
                    domainKey: summary.DomainKey,
                    title: summary.Title,
                    description: summary.Description,
                    classes: classesForDomain);

                domains.Add(domainEntry);
            }

            // Flat class list is the union of all model classes returned by the extractor.
            var catalog = new DomainCatalog(domains, modelClasses);

            return catalog;
        }

        #endregion
    }
}
