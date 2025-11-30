using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// High-level orchestration for IDX-066. This implementation focuses on
    /// sequencing, safety, and catalog rules and delegates parsing to
    /// IModelMetadataSource and IDomainMetadataSource.
    /// </summary>
    public class TitleDescriptionRefinementOrchestrator : ITitleDescriptionRefinementOrchestrator
    {
        private readonly ITitleDescriptionReviewService _reviewService;
        private readonly IModelMetadataSource _modelSource;
        private readonly IDomainMetadataSource _domainSource;
        private readonly ITitleDescriptionRefinementCatalogStore _catalogStore;
        private readonly IContentHashService _hashService;
        private readonly IAdminLogger _logger;
        private readonly string _indexVersion;

        public TitleDescriptionRefinementOrchestrator(
            ITitleDescriptionReviewService reviewService,
            IModelMetadataSource modelSource,
            IDomainMetadataSource domainSource,
            ITitleDescriptionRefinementCatalogStore catalogStore,
            IContentHashService hashService,
            IAdminLogger logger)
        {
            _reviewService = reviewService ?? throw new ArgumentNullException(nameof(reviewService));
            _modelSource = modelSource ?? throw new ArgumentNullException(nameof(modelSource));
            _domainSource = domainSource ?? throw new ArgumentNullException(nameof(domainSource));
            _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
            _hashService = hashService ?? throw new ArgumentNullException(nameof(hashService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _indexVersion = "1";
        }

        public async Task RunAsync(
            IReadOnlyList<DiscoveredFile> files,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> resources,
            CancellationToken cancellationToken)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));
            if (resources == null) throw new ArgumentNullException(nameof(resources));

            var catalog = await _catalogStore.LoadAsync(cancellationToken).ConfigureAwait(false);

            _logger.Trace($"[TitleDescriptionRefinementOrchestrator_RunAsync] Starting model pass for {files.Count} files.");

            var models = await _modelSource.GetModelsAsync(files, resources, cancellationToken).ConfigureAwait(false);

            // Duplicate model-name detection across the entire input set.
            var duplicateGroups = models
                .GroupBy(m => m.ClassName)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
                .ToList();

            foreach (var group in duplicateGroups)
            {
                foreach (var model in group)
                {
                    model.Errors.Add($"Duplicate model name '{model.ClassName}' across files.");
                }
            }

            foreach (var model in models)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileHash = await _hashService.ComputeFileHashAsync(model.FullPath).ConfigureAwait(false);

                var existingFailure = catalog.Failures.FirstOrDefault(e =>
                    e.Kind == CatalogEntryKind.Model &&
                    string.Equals(e.File, model.FullPath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(e.SymbolName, model.ClassName, StringComparison.Ordinal));

                // If we previously recorded a failure for this model and the
                // file hash has not changed, skip re-processing.
                if (!model.HasErrors &&
                    existingFailure != null &&
                    string.Equals(existingFailure.FileHash, fileHash, StringComparison.Ordinal))
                {
                    continue;
                }

                var existingRefined = catalog.Refined.FirstOrDefault(e =>
                    e.Kind == CatalogEntryKind.Model &&
                    string.Equals(e.File, model.FullPath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(e.SymbolName, model.ClassName, StringComparison.Ordinal));

                if (!model.HasErrors &&
                    existingRefined != null &&
                    string.Equals(existingRefined.FileHash, fileHash, StringComparison.Ordinal) &&
                    existingRefined.IndexVersion == _indexVersion)
                {
                    // Already refined at this index version; skip.
                    continue;
                }

                if (model.HasErrors)
                {
                    // Structural issues: treat as failure, no LLM call.
                    var reason = string.Join("; ", model.Errors);

                    catalog.Failures.RemoveAll(e =>
                        e.Kind == CatalogEntryKind.Model &&
                        e.File == model.FullPath &&
                        e.SymbolName == model.ClassName);

                    catalog.Failures.Add(new TitleDescriptionCatalogEntry
                    {
                        Kind = CatalogEntryKind.Model,
                        RepoId = model.RepoId,
                        File = model.FullPath,
                        FileHash = fileHash,
                        SymbolName = model.ClassName,
                        IndexVersion = _indexVersion,
                        Timestamp = DateTime.UtcNow,
                        OriginalTitle = model.Title,
                        OriginalDescription = model.Description,
                        OriginalHelp = model.Help,
                        TitleResourceKey = model.TitleResourceKey,
                        DescriptionResourceKey = model.DescriptionResourceKey,
                        HelpResourceKey = model.HelpResourceKey,
                        ReasonOrNotes = reason
                    });

                    await _catalogStore.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var contextBlob = BuildModelContextBlob(model);

                _logger.Trace($"TitleDescriptionRefinementOrchestrator_RunAsync] Reviewing model {model.ClassName} in {model.FullPath}.");

                var review = await _reviewService.ReviewAsync(
                    SummaryObjectKind.Model,
                    model.ClassName,
                    model.Title,
                    model.Description,
                    model.Help,
                    contextBlob,
                    cancellationToken).ConfigureAwait(false);

                if (!review.IsSuccessful)
                {
                    catalog.Failures.RemoveAll(e => e.Kind == CatalogEntryKind.Model && e.File == model.FullPath && e.SymbolName == model.ClassName);

                    catalog.Failures.Add(new TitleDescriptionCatalogEntry
                    {
                        Kind = CatalogEntryKind.Model,
                        RepoId = model.RepoId,
                        File = model.FullPath,
                        FileHash = fileHash,
                        SymbolName = model.ClassName,
                        IndexVersion = _indexVersion,
                        Timestamp = DateTime.UtcNow,
                        OriginalTitle = model.Title,
                        OriginalDescription = model.Description,
                        OriginalHelp = model.Help,
                        TitleResourceKey = model.TitleResourceKey,
                        DescriptionResourceKey = model.DescriptionResourceKey,
                        HelpResourceKey = model.HelpResourceKey,
                        ReasonOrNotes = review.FailureReason
                    });

                    await _catalogStore.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (review.RequiresAttention)
                {
                    catalog.Warnings.RemoveAll(e => e.Kind == CatalogEntryKind.Model && e.File == model.FullPath && e.SymbolName == model.ClassName);

                    catalog.Warnings.Add(new TitleDescriptionCatalogEntry
                    {
                        Kind = CatalogEntryKind.Model,
                        RepoId = model.RepoId,
                        File = model.FullPath,
                        FileHash = fileHash,
                        SymbolName = model.ClassName,
                        IndexVersion = _indexVersion,
                        Timestamp = DateTime.UtcNow,
                        OriginalTitle = model.Title,
                        OriginalDescription = model.Description,
                        OriginalHelp = model.Help,
                        TitleResourceKey = model.TitleResourceKey,
                        DescriptionResourceKey = model.DescriptionResourceKey,
                        HelpResourceKey = model.HelpResourceKey,
                        ReasonOrNotes = review.Notes
                    });

                    await _catalogStore.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                catalog.Refined.RemoveAll(e => e.Kind == CatalogEntryKind.Model && e.File == model.FullPath && e.SymbolName == model.ClassName);

                catalog.Refined.Add(new TitleDescriptionCatalogEntry
                {
                    Kind = CatalogEntryKind.Model,
                    RepoId = model.RepoId,
                    File = model.FullPath,
                    FileHash = fileHash,
                    SymbolName = model.ClassName,
                    IndexVersion = _indexVersion,
                    Timestamp = DateTime.UtcNow,
                    OriginalTitle = model.Title,
                    OriginalDescription = model.Description,
                    OriginalHelp = model.Help,
                    RefinedTitle = review.RefinedTitle,
                    RefinedDescription = review.RefinedDescription,
                    RefinedHelp = review.RefinedHelp,
                    TitleResourceKey = model.TitleResourceKey,
                    DescriptionResourceKey = model.DescriptionResourceKey,
                    HelpResourceKey = model.HelpResourceKey,
                    ReasonOrNotes = review.Notes
                });

                await _catalogStore.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
            }

            // Domain pass: only if there are no model failures for this run.
            if (catalog.Failures.Any(e => e.Kind == CatalogEntryKind.Model))
            {
                _logger.Trace("[TitleDescriptionRefinementOrchestrator_RunAsync] Skipping domain pass due to model failures.");
                return;
            }

            _logger.Trace("[TitleDescriptionRefinementOrchestrator_RunAsync] Starting domain pass.");

            var domains = await _domainSource.GetDomainsAsync(files, models, cancellationToken).ConfigureAwait(false);

            foreach (var domain in domains)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileHash = await _hashService.ComputeFileHashAsync(domain.FullPath).ConfigureAwait(false);
                var contextBlob = BuildDomainContextBlob(domain);

                var review = await _reviewService.ReviewAsync(
                    SummaryObjectKind.Domain,
                    domain.ClassName,
                    domain.Name,
                    domain.Description,
                    null,
                    contextBlob,
                    cancellationToken).ConfigureAwait(false);

                if (!review.IsSuccessful)
                {
                    catalog.Failures.Add(new TitleDescriptionCatalogEntry
                    {
                        Kind = CatalogEntryKind.Domain,
                        RepoId = domain.RepoId,
                        File = domain.FullPath,
                        FileHash = fileHash,
                        SymbolName = domain.ClassName,
                        IndexVersion = _indexVersion,
                        Timestamp = DateTime.UtcNow,
                        OriginalTitle = domain.Name,
                        OriginalDescription = domain.Description,
                        ReasonOrNotes = review.FailureReason
                    });

                    await _catalogStore.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (review.RequiresAttention)
                {
                    catalog.Warnings.Add(new TitleDescriptionCatalogEntry
                    {
                        Kind = CatalogEntryKind.Domain,
                        RepoId = domain.RepoId,
                        File = domain.FullPath,
                        FileHash = fileHash,
                        SymbolName = domain.ClassName,
                        IndexVersion = _indexVersion,
                        Timestamp = DateTime.UtcNow,
                        OriginalTitle = domain.Name,
                        OriginalDescription = domain.Description,
                        ReasonOrNotes = review.Notes
                    });
                }

                catalog.Domains.RemoveAll(d => d.File == domain.FullPath && d.SymbolName == domain.ClassName);

                catalog.Domains.Add(new DomainCatalogEntry
                {
                    RepoId = domain.RepoId,
                    File = domain.FullPath,
                    FileHash = fileHash,
                    SymbolName = domain.ClassName,
                    DomainKey = domain.DomainKey,
                    IndexVersion = _indexVersion,
                    Timestamp = DateTime.UtcNow,
                    OriginalTitle = domain.Name,
                    OriginalDescription = domain.Description,
                    RefinedTitle = review.RequiresAttention ? domain.Name : review.RefinedTitle,
                    RefinedDescription = review.RequiresAttention ? domain.Description : review.RefinedDescription,
                    Entities = domain.Entities
                });

                await _catalogStore.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
            }
        }

        private static string BuildModelContextBlob(ModelMetadata model)
        {
            if (model.Fields == null || model.Fields.Count == 0)
            {
                return null;
            }

            var sb = new StringBuilder();
            sb.AppendLine("KEY FIELDS (CONTEXT ONLY):");
            foreach (var field in model.Fields)
            {
                var label = string.IsNullOrWhiteSpace(field.Label) ? field.PropertyName : field.Label;
                sb.Append("- ").Append(label).Append(": ");
                if (!string.IsNullOrWhiteSpace(field.Help))
                {
                    sb.Append(field.Help);
                }
                else
                {
                    sb.Append("(no additional help text)");
                }
                sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("Use these fields only as context to understand the model's purpose.");
            sb.AppendLine("Do NOT list all fields in the final description or help text.");
            return sb.ToString();
        }

        private static string BuildDomainContextBlob(DomainMetadata domain)
        {
            if (domain.Entities == null || domain.Entities.Count == 0)
            {
                return "This domain currently has no associated entities in this run.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("This domain currently contains the following entities (context only, do not list them all in the final description):");
            foreach (var entity in domain.Entities)
            {
                sb.Append("- ").Append(entity.SymbolName).Append(": ")
                  .Append(entity.Title).Append(" — ")
                  .Append(entity.Description).AppendLine();
            }
            sb.AppendLine("Use this list only as context to understand the domain's scope.");
            return sb.ToString();
        }
    }
}
