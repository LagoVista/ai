using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI;
using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Services;
using LagoVista.AI.Quality.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Quality.Services
{
    /// <summary>
    /// High-level orchestration for IDX-066. This implementation focuses on
    /// sequencing, safety, and catalog rules and delegates parsing to
    /// IModelMetadataSource and IDomainMetadataSource.
    ///
    /// After refactoring, this orchestrator now calls IStructuredTextLlmService
    /// directly for title/description refinement instead of chaining through
    /// multiple intermediate services.
    /// </summary>
    public class TitleDescriptionRefinementOrchestrator : ITitleDescriptionRefinementOrchestrator
    {
        private readonly IModelMetadataSource _modelSource;
        private readonly IDomainMetadataSource _domainSource;
        private readonly ITitleDescriptionRefinementCatalogStore _catalogStore;
        private readonly IContentHashService _hashService;
        private readonly IResxUpdateService _resxUpdateService;
        private readonly IDomainDescriptorUpdateService _domainDescriptorUpdateService;
        private readonly IStructuredTextLlmService _structuredTextLlmService;
        private readonly IAdminLogger _logger;
        private readonly string _indexVersion;

        public TitleDescriptionRefinementOrchestrator(
            IModelMetadataSource modelSource,
            IDomainMetadataSource domainSource,
            ITitleDescriptionRefinementCatalogStore catalogStore,
            IContentHashService hashService,
            IResxUpdateService resxUpdateService,
            IDomainDescriptorUpdateService domainDescriptorUpdateService,
            IStructuredTextLlmService structuredTextLlmService,
            IAdminLogger logger)
        {
            _modelSource = modelSource ?? throw new ArgumentNullException(nameof(modelSource));
            _domainSource = domainSource ?? throw new ArgumentNullException(nameof(domainSource));
            _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
            _hashService = hashService ?? throw new ArgumentNullException(nameof(hashService));
            _resxUpdateService = resxUpdateService ?? throw new ArgumentNullException(nameof(resxUpdateService));
            _domainDescriptorUpdateService = domainDescriptorUpdateService ?? throw new ArgumentNullException(nameof(domainDescriptorUpdateService));
            _structuredTextLlmService = structuredTextLlmService ?? throw new ArgumentNullException(nameof(structuredTextLlmService));
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

            // Build a lookup from resource key -> full RESX path.
            var resourceKeyToResxPath = BuildResourceKeyToResxPathMap(resources);

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
                if (model.FullPath.ToLower().Contains("test"))
                    continue;

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

                    ClearModelEntries(catalog, model.FullPath, model.ClassName);

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

                _logger.Trace($"[TitleDescriptionRefinementOrchestrator_RunAsync] Reviewing model {model.ClassName} in {model.FullPath}.");

                var review = await ReviewAsync(
                    SummaryObjectKind.Model,
                    model.ClassName,
                    model.Title,
                    model.Description,
                    model.Help,
                    contextBlob,
                    cancellationToken).ConfigureAwait(false);

                if (!review.IsSuccessful)
                {
                    ClearModelEntries(catalog, model.FullPath, model.ClassName);

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
                    ClearModelEntries(catalog, model.FullPath, model.ClassName);

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

                if (!review.HasChanges)
                {
                    // Successful, but LLM indicated no changes.
                    ClearModelEntries(catalog, model.FullPath, model.ClassName);

                    catalog.Skipped.Add(new TitleDescriptionCatalogEntry
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
                        ReasonOrNotes = "LLM reported no changes."
                    });

                    await _catalogStore.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // Successful review with changes and no attention required.
                // Determine which RESX-backed keys actually changed and write them immediately.
                var missingKeys = new List<string>();
                var perResxUpdates = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

                void Consider(string resourceKey, string original, string refined)
                {
                    if (string.IsNullOrWhiteSpace(resourceKey))
                    {
                        return;
                    }

                    var originalValue = original ?? string.Empty;
                    var refinedValue = refined ?? string.Empty;

                    if (string.Equals(originalValue, refinedValue, StringComparison.Ordinal))
                    {
                        return;
                    }

                    if (!resourceKeyToResxPath.TryGetValue(resourceKey, out var resxPath))
                    {
                        missingKeys.Add(resourceKey);
                        return;
                    }

                    if (!perResxUpdates.TryGetValue(resxPath, out var map))
                    {
                        map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        perResxUpdates[resxPath] = map;
                    }

                    map[resourceKey] = refinedValue;
                }

                Consider(model.TitleResourceKey, model.Title, review.RefinedTitle);
                Consider(model.DescriptionResourceKey, model.Description, review.RefinedDescription);
                Consider(model.HelpResourceKey, model.Help, review.RefinedHelp);

                if (missingKeys.Count > 0)
                {
                    var reason = $"Unable to resolve RESX path for resource keys: {string.Join(", ", missingKeys)}.";

                    ClearModelEntries(catalog, model.FullPath, model.ClassName);

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
                        RefinedTitle = review.RefinedTitle,
                        RefinedDescription = review.RefinedDescription,
                        RefinedHelp = review.RefinedHelp,
                        TitleResourceKey = model.TitleResourceKey,
                        DescriptionResourceKey = model.DescriptionResourceKey,
                        HelpResourceKey = model.HelpResourceKey,
                        ReasonOrNotes = reason
                    });

                    await _catalogStore.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (perResxUpdates.Count == 0)
                {
                    // LLM changed something conceptually, but nothing on RESX-backed keys.
                    // Record as a warning and keep suggestions in the catalog only.
                    ClearModelEntries(catalog, model.FullPath, model.ClassName);

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
                        RefinedTitle = review.RefinedTitle,
                        RefinedDescription = review.RefinedDescription,
                        RefinedHelp = review.RefinedHelp,
                        TitleResourceKey = model.TitleResourceKey,
                        DescriptionResourceKey = model.DescriptionResourceKey,
                        HelpResourceKey = model.HelpResourceKey,
                        ReasonOrNotes = "LLM produced changes but no RESX-backed keys were configured for this model. Suggestions recorded only in catalog."
                    });

                    await _catalogStore.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var resxWriteFailed = false;
                string resxFailureReason = null;

                foreach (var kvp in perResxUpdates)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var resxPath = kvp.Key;
                    var updates = (IReadOnlyDictionary<string, string>)kvp.Value;

                    try
                    {
                        await _resxUpdateService.ApplyUpdatesAsync(resxPath, updates, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        resxWriteFailed = true;
                        resxFailureReason = $"RESX update failed for '{resxPath}': {ex.Message}";
                        _logger.Trace($"[TitleDescriptionRefinementOrchestrator_RunAsync] {resxFailureReason}");
                    }
                }

                if (resxWriteFailed)
                {
                    ClearModelEntries(catalog, model.FullPath, model.ClassName);

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
                        RefinedTitle = review.RefinedTitle,
                        RefinedDescription = review.RefinedDescription,
                        RefinedHelp = review.RefinedHelp,
                        TitleResourceKey = model.TitleResourceKey,
                        DescriptionResourceKey = model.DescriptionResourceKey,
                        HelpResourceKey = model.HelpResourceKey,
                        ReasonOrNotes = resxFailureReason ?? "RESX update failed. See logs for details."
                    });

                    await _catalogStore.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // RESX write succeeded; record as refined.
                ClearModelEntries(catalog, model.FullPath, model.ClassName);

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

                var originalHash = await _hashService.ComputeFileHashAsync(domain.FullPath).ConfigureAwait(false);
                var contextBlob = BuildDomainContextBlob(domain);

                var review = await ReviewAsync(
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
                        FileHash = originalHash,
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
                        FileHash = originalHash,
                        SymbolName = domain.ClassName,
                        IndexVersion = _indexVersion,
                        Timestamp = DateTime.UtcNow,
                        OriginalTitle = domain.Name,
                        OriginalDescription = domain.Description,
                        ReasonOrNotes = review.Notes
                    });

                    // Do not touch disk; record catalog only with original values.
                    catalog.Domains.RemoveAll(d => d.File == domain.FullPath && d.SymbolName == domain.ClassName);

                    catalog.Domains.Add(new DomainCatalogEntry
                    {
                        RepoId = domain.RepoId,
                        File = domain.FullPath,
                        FileHash = originalHash,
                        SymbolName = domain.ClassName,
                        DomainKey = domain.DomainKey,
                        IndexVersion = _indexVersion,
                        Timestamp = DateTime.UtcNow,
                        OriginalTitle = domain.Name,
                        OriginalDescription = domain.Description,
                        RefinedTitle = domain.Name,
                        RefinedDescription = domain.Description,
                        Entities = domain.Entities
                    });

                    await _catalogStore.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!review.HasChanges)
                {
                    // Successful, no changes; record as skipped and keep originals.
                    catalog.Skipped.Add(new TitleDescriptionCatalogEntry
                    {
                        Kind = CatalogEntryKind.Domain,
                        RepoId = domain.RepoId,
                        File = domain.FullPath,
                        FileHash = originalHash,
                        SymbolName = domain.ClassName,
                        IndexVersion = _indexVersion,
                        Timestamp = DateTime.UtcNow,
                        OriginalTitle = domain.Name,
                        OriginalDescription = domain.Description,
                        ReasonOrNotes = "LLM reported no changes."
                    });

                    catalog.Domains.RemoveAll(d => d.File == domain.FullPath && d.SymbolName == domain.ClassName);

                    catalog.Domains.Add(new DomainCatalogEntry
                    {
                        RepoId = domain.RepoId,
                        File = domain.FullPath,
                        FileHash = originalHash,
                        SymbolName = domain.ClassName,
                        DomainKey = domain.DomainKey,
                        IndexVersion = _indexVersion,
                        Timestamp = DateTime.UtcNow,
                        OriginalTitle = domain.Name,
                        OriginalDescription = domain.Description,
                        RefinedTitle = domain.Name,
                        RefinedDescription = domain.Description,
                        Entities = domain.Entities
                    });

                    await _catalogStore.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // Happy path for domains: successful review with changes and no attention required.
                try
                {
                    await _domainDescriptorUpdateService.UpdateAsync(domain, review, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Trace($"[TitleDescriptionRefinementOrchestrator_RunAsync] Domain update failed for {domain.ClassName} in {domain.FullPath}: {ex.Message}");

                    catalog.Failures.Add(new TitleDescriptionCatalogEntry
                    {
                        Kind = CatalogEntryKind.Domain,
                        RepoId = domain.RepoId,
                        File = domain.FullPath,
                        FileHash = originalHash,
                        SymbolName = domain.ClassName,
                        IndexVersion = _indexVersion,
                        Timestamp = DateTime.UtcNow,
                        OriginalTitle = domain.Name,
                        OriginalDescription = domain.Description,
                        ReasonOrNotes = "Domain descriptor update failed. See logs for details."
                    });

                    await _catalogStore.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // After a successful write, recompute the hash so future runs can skip.
                var newHash = await _hashService.ComputeFileHashAsync(domain.FullPath).ConfigureAwait(false);

                catalog.Domains.RemoveAll(d => d.File == domain.FullPath && d.SymbolName == domain.ClassName);

                catalog.Domains.Add(new DomainCatalogEntry
                {
                    RepoId = domain.RepoId,
                    File = domain.FullPath,
                    FileHash = newHash,
                    SymbolName = domain.ClassName,
                    DomainKey = domain.DomainKey,
                    IndexVersion = _indexVersion,
                    Timestamp = DateTime.UtcNow,
                    OriginalTitle = domain.Name,
                    OriginalDescription = domain.Description,
                    RefinedTitle = review.RefinedTitle,
                    RefinedDescription = review.RefinedDescription,
                    Entities = domain.Entities
                });

                await _catalogStore.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Perform a single title/description review by calling the generic
        /// structured text LLM service. This method is responsible for:
        /// - Building a system prompt,
        /// - Building a compact JSON payload from the inputs and context,
        /// - Calling IStructuredTextLlmService,
        /// - Applying guard rails and enrichment (HasChanges, RequiresAttention, AdditionalConfiguration, etc.).
        /// </summary>
        private async Task<TitleDescriptionReviewResult> ReviewAsync(
            SummaryObjectKind kind,
            string symbolName,
            string title,
            string description,
            string help,
            string contextBlob,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(symbolName))
            {
                throw new ArgumentException("Symbol name is required.", nameof(symbolName));
            }

            var systemPrompt = BuildTitleDescriptionSystemPrompt();

            var payload = new
            {
                kind = kind.ToString(),
                symbolName,
                title,
                description,
                help,
                extraContext = contextBlob
            };

            var inputText = JsonConvert.SerializeObject(payload, Formatting.None);

            InvokeResult<TitleDescriptionReviewResult> invokeResult;

            try
            {
                invokeResult = await _structuredTextLlmService.ExecuteAsync<TitleDescriptionReviewResult>(
                        systemPrompt,
                        inputText,
                        model: null,
                        operationName: "TitleDescriptionReview",
                        correlationId: symbolName,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.AddException("TitleDescriptionRefinementOrchestrator_ReviewAsync", ex);

                return TitleDescriptionReviewResult.FromError(
                    kind,
                    symbolName,
                    title,
                    description,
                    help,
                    ex.Message);
            }

            if (!invokeResult.Successful || invokeResult.Result == null)
            {
                var reason = invokeResult.Successful
                    ? "LLM returned null result."
                    : string.Join("; ", invokeResult.Errors.Select(e => e.Message));

                return TitleDescriptionReviewResult.FromError(
                    kind,
                    symbolName,
                    title,
                    description,
                    help,
                    reason);
            }

            var llmResult = invokeResult.Result;

            // Build the final, enriched result that includes originals and guard-rails.
            var final = new TitleDescriptionReviewResult
            {
                Kind = kind,
                SymbolName = symbolName,
                OriginalTitle = title,
                OriginalDescription = description,
                OriginalHelp = help,

                Title = string.IsNullOrWhiteSpace(llmResult?.Title) ? title : llmResult.Title,
                Description = string.IsNullOrWhiteSpace(llmResult?.Description) ? description : llmResult.Description,
                Help = llmResult?.Help ?? help,

                HasChanges = llmResult?.HasChanges ?? false,
                RequiresAttention = llmResult?.RequiresAttention ?? false,
                IsError = false
            };

            if (llmResult?.Warnings != null)
            {
                foreach (var warning in llmResult.Warnings)
                {
                    if (!string.IsNullOrWhiteSpace(warning))
                    {
                        final.Warnings.Add(warning.Trim());
                    }
                }
            }

            // Guard rail: if the LLM produced empty title/description, fall back to
            // originals but mark this item as requiring attention.
            if (string.IsNullOrWhiteSpace(llmResult?.Title) ||
                string.IsNullOrWhiteSpace(llmResult?.Description))
            {
                final.HasChanges = false;
                final.RequiresAttention = true;
                final.Warnings.Add("LLM returned empty title and/or description; original values were preserved.");
            }

            // Derive HasChanges if the LLM did not set it or set it incorrectly.
            if (!final.HasChanges)
            {
                if (!string.Equals(final.Title ?? string.Empty, title ?? string.Empty, StringComparison.Ordinal) ||
                    !string.Equals(final.Description ?? string.Empty, description ?? string.Empty, StringComparison.Ordinal) ||
                    !string.Equals(final.Help ?? string.Empty, help ?? string.Empty, StringComparison.Ordinal))
                {
                    final.HasChanges = true;
                }
            }

            // Populate AdditionalConfiguration from warnings so the catalog can store
            // a single human-readable string when appropriate.
            if (final.Warnings.Count > 0)
            {
                final.Notes = string.Join("; ", final.Warnings);
            }

            return final;
        }

        /// <summary>
        /// System prompt used for all title/description refinement calls.
        /// Mirrors the earlier HttpLlmTitleDescriptionClient behavior but is now
        /// owned directly by the orchestrator.
        /// </summary>
        private static string BuildTitleDescriptionSystemPrompt()
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are an expert technical editor for a large enterprise codebase.");
            sb.AppendLine("Your job is to refine domain and model titles/descriptions/help text used in UI metadata.");
            sb.AppendLine();
            sb.AppendLine("Requirements:");
            sb.AppendLine("- Preserve the original semantic meaning.");
            sb.AppendLine("- Improve clarity, grammar, and spelling.");
            sb.AppendLine("- Keep text concise and user-facing (no internal jargon).");
            sb.AppendLine("- Do NOT invent features or behavior that are not implied by the input.");
            sb.AppendLine();
            sb.AppendLine("You must respond with data that can be interpreted as a TitleDescriptionReviewResult instance.");
            sb.AppendLine("Each property of TitleDescriptionReviewResult must be populated according to its meaning.");

            return sb.ToString();
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

        private static Dictionary<string, string> BuildResourceKeyToResxPathMap(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> resources)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var resxEntry in resources)
            {
                var resxPathRaw = resxEntry.Key;
                if (string.IsNullOrWhiteSpace(resxPathRaw))
                {
                    continue;
                }

                // Caller provides the full path to the RESX file.
                var resxPath = Path.GetFullPath(resxPathRaw);

                var keyMap = resxEntry.Value;
                if (keyMap == null)
                {
                    continue;
                }

                foreach (var key in keyMap.Keys)
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    if (!map.ContainsKey(key))
                    {
                        map[key] = resxPath;
                    }
                }
            }

            return map;
        }

        private static void ClearModelEntries(TitleDescriptionCatalog catalog, string file, string symbolName)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (symbolName == null) throw new ArgumentNullException(nameof(symbolName));

            catalog.Failures.RemoveAll(e =>
                e.Kind == CatalogEntryKind.Model &&
                string.Equals(e.File, file, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.SymbolName, symbolName, StringComparison.Ordinal));

            catalog.Warnings.RemoveAll(e =>
                e.Kind == CatalogEntryKind.Model &&
                string.Equals(e.File, file, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.SymbolName, symbolName, StringComparison.Ordinal));

            catalog.Skipped.RemoveAll(e =>
                e.Kind == CatalogEntryKind.Model &&
                string.Equals(e.File, file, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.SymbolName, symbolName, StringComparison.Ordinal));

            catalog.Refined.RemoveAll(e =>
                e.Kind == CatalogEntryKind.Model &&
                string.Equals(e.File, file, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.SymbolName, symbolName, StringComparison.Ordinal));
        }
    }
}
