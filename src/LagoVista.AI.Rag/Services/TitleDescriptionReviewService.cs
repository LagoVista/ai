using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// High-level title/description refinement service for IDX-066.
    ///
    /// Responsibilities:
    /// - Build a TitleDescriptionReviewRequest from basic parameters.
    /// - Construct a blended context blob combining the core fields and any
    ///   orchestrator-provided context (hybrid responsibility per IDX-066).
    /// - Call the ITitleDescriptionLlmClient to obtain a structured result.
    /// - Apply guard rails (fallback to original on suspicious output).
    /// - Normalize HasChanges / RequiresAttention and merge warnings.
    ///
    /// This service does NOT talk to the filesystem or catalog; that is handled
    /// by the TitleDescriptionRefinementOrchestrator and catalog store.
    /// </summary>
    public class TitleDescriptionReviewService : ITitleDescriptionReviewService
    {
        private readonly ITitleDescriptionLlmClient _llmClient;
        private readonly IAdminLogger _logger;

        public TitleDescriptionReviewService(
            ITitleDescriptionLlmClient llmClient,
            IAdminLogger logger)
        {
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<TitleDescriptionReviewResult> ReviewAsync(
            SummaryObjectKind kind,
            string symbolName,
            string title,
            string description,
            string help,
            string contextBlob,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbolName))
            {
                throw new ArgumentException("Symbol name is required.", nameof(symbolName));
            }

            var request = new TitleDescriptionReviewRequest
            {
                Kind = kind,
                SymbolName = symbolName,
                Title = title,
                Description = description,
                Help = help,
                Model = HttpLlmTitleDescriptionClient.DefaultModel,

                // Domain and field context will be populated by higher-level callers
                // as those pieces of metadata become available. For now they are
                // optional and may remain empty.
                DomainKey = null,
                DomainName = null,
                DomainDescription = null,
                Fields = new List<ModelFieldMetadata>()
            };

            // Hybrid approach (Q1 = C): merge core structured fields with any
            // orchestrator-provided context into a single JSON blob.
            request.ContextBlob = BuildContextBlob(request, contextBlob);

            TitleDescriptionReviewResult llmResult;

            try
            {
                llmResult = await _llmClient.ReviewAsync(request, cancellationToken).ConfigureAwait(false);
                if(llmResult.IsError)
                {
                    return TitleDescriptionReviewResult.FromError(
                     kind,
                     symbolName,
                     title,
                     description,
                     help,
                     llmResult.FailureReason);
                    }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is InvalidOperationException)
            {
                _logger.AddException("TitleDescriptionReviewService_ReviewAsync", ex);

                return TitleDescriptionReviewResult.FromError(
                    kind,
                    symbolName,
                    title,
                    description,
                    help,
                    ex.Message);
            }

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

            // Guard rail: if the LLM produced empty title/description, we fall back to
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

            // Populate Notes from warnings so the orchestrator/catalog can store
            // a single human-readable string when appropriate.
            if (final.Warnings.Count > 0)
            {
                final.Notes = string.Join("; ", final.Warnings);
            }

            return final;
        }

        /// <summary>
        /// Build the blended context blob for the LLM. This combines the structured
        /// fields into a single JSON object so the prompt can stay stable even if
        /// we evolve the request shape over time. Orchestrator-provided context is
        /// attached as an "extraContext" field.
        /// </summary>
        private static string BuildContextBlob(TitleDescriptionReviewRequest request, string externalContext)
        {
            var context = new
            {
                kind = request.Kind.ToString(),
                symbolName = request.SymbolName,
                title = request.Title,
                description = request.Description,
                help = request.Help,
                domain = string.IsNullOrWhiteSpace(request.DomainKey)
                    ? null
                    : new
                    {
                        key = request.DomainKey,
                        name = request.DomainName,
                        description = request.DomainDescription
                    },
                fields = request.Fields,
                extraContext = externalContext
            };

            return JsonConvert.SerializeObject(context, Formatting.None);
        }
    }
}
