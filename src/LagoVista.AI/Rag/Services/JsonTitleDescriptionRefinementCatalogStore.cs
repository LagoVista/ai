using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// JSON-backed implementation of ITitleDescriptionRefinementCatalogStore.
    ///
    /// Uses a single file at the provided path (DomainCatalogPath from IngestionConfig).
    /// The file format is:
    ///   [JSON serialized TitleDescriptionCatalog]
    ///   ----- IDX-066 SUMMARY -----
    ///   [human-readable footer lines]
    ///
    /// LoadAsync parses only the JSON portion and ignores the footer.
    /// SaveAsync rewrites both JSON and footer on each call.
    /// </summary>
    public class JsonTitleDescriptionRefinementCatalogStore : ITitleDescriptionRefinementCatalogStore
    {
        private readonly string _catalogPath;
        private readonly IAdminLogger _logger;

        private const string FooterMarker = "----- IDX-066 SUMMARY -----";

        public const string CatalogFileName = "domain-model-index.json";

        public JsonTitleDescriptionRefinementCatalogStore(IngestionConfig config, IAdminLogger logger)
        {
            _catalogPath =  Path.Combine(config.Ingestion.SourceRoot, config.DomainCatalogPath, CatalogFileName);

            if (string.IsNullOrWhiteSpace(_catalogPath))
            {
                throw new ArgumentException("Catalog path must be provided within IngestionConfig.", nameof(IngestionConfig.DomainCatalogPath));
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TitleDescriptionCatalog> LoadAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!File.Exists(_catalogPath))
                {
                    return new TitleDescriptionCatalog();
                }

                var text = await File.ReadAllTextAsync(_catalogPath, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return new TitleDescriptionCatalog();
                }

                var idx = text.IndexOf(FooterMarker, StringComparison.Ordinal);
                var jsonPart = idx > 0 ? text.Substring(0, idx).TrimEnd() : text;

                var catalog = JsonConvert.DeserializeObject<TitleDescriptionCatalog>(jsonPart);
                return catalog ?? new TitleDescriptionCatalog();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.AddException("JsonTitleDescriptionRefinementCatalogStore_LoadAsync", ex);
                return new TitleDescriptionCatalog();
            }
        }

        public async Task SaveAsync(TitleDescriptionCatalog catalog, CancellationToken cancellationToken)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            try
            {
                var directory = Path.GetDirectoryName(_catalogPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                catalog.GeneratedOn = DateTime.UtcNow;

                var json = JsonConvert.SerializeObject(catalog, Formatting.Indented);
                var footer = BuildFooter(catalog);
                var content = json + Environment.NewLine + footer;

                await File.WriteAllTextAsync(_catalogPath, content, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.AddException("JsonTitleDescriptionRefinementCatalogStore_SaveAsync", ex);
            }
        }

        private static string BuildFooter(TitleDescriptionCatalog catalog)
        {
            var refinedModels = catalog.Refined.Count(e => e.Kind == CatalogEntryKind.Model);
            var refinedDomains = catalog.Refined.Count(e => e.Kind == CatalogEntryKind.Domain);
            var warningModels = catalog.Warnings.Count(e => e.Kind == CatalogEntryKind.Model);
            var warningDomains = catalog.Warnings.Count(e => e.Kind == CatalogEntryKind.Domain);
            var failureModels = catalog.Failures.Count(e => e.Kind == CatalogEntryKind.Model);
            var failureDomains = catalog.Failures.Count(e => e.Kind == CatalogEntryKind.Domain);
            var skipped = catalog.Skipped.Count;
            var domainSummaries = catalog.Domains.Count;

            var indexVersions = catalog.Refined
                .Concat(catalog.Warnings)
                .Concat(catalog.Failures)
                .Select(e => e.IndexVersion)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .OrderBy(v => v)
                .ToArray();

            var sb = new StringBuilder();
            sb.AppendLine(FooterMarker);
            sb.AppendLine($"Generated (UTC): {catalog.GeneratedOn:O}");
            if (indexVersions.Length > 0)
            {
                sb.AppendLine("IndexVersions: " + string.Join(", ", indexVersions));
            }

            sb.AppendLine($"Refined Models: {refinedModels}");
            sb.AppendLine($"Refined Domains: {refinedDomains}");
            sb.AppendLine($"Warnings (Models): {warningModels}");
            sb.AppendLine($"Warnings (Domains): {warningDomains}");
            sb.AppendLine($"Failures (Models): {failureModels}");
            sb.AppendLine($"Failures (Domains): {failureDomains}");
            sb.AppendLine($"Skipped Files: {skipped}");
            sb.AppendLine($"Domain Summaries: {domainSummaries}");

            return sb.ToString();
        }
    }
}
