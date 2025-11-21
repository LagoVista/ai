using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// Core implementation for Step 7 of the indexing pipeline.
    ///
    /// Given a single <see cref="IndexFileContext"/> (one physical file), this
    /// service produces a sequence of <see cref="NormalizedChunk"/> objects that
    /// are ready to be embedded.
    ///
    /// The public entry point is intentionally small and delegates to a series
    /// of clearly named private methods that mirror the Step 7 checklist.
    /// </summary>
    public sealed class DefaultNormalizedChunkBuilderService : IDefaultNormalizedChunkBuilderService
    {
        private readonly IChunkerServices _chunkerServices;

        /// <summary>
        /// Pre-built domain/model catalog for the current indexing run.
        /// Populated by the orchestrator before calling <see cref="BuildAsync"/>.
        /// </summary>
        public DomainModelCatalog DomainModelCatalog { get; set; }

        public DefaultNormalizedChunkBuilderService(IChunkerServices chunkerServices)
        {
            _chunkerServices = chunkerServices ?? throw new ArgumentNullException(nameof(chunkerServices));
        }

        /// <summary>
        /// High-level workflow for Step 7.
        ///
        /// The goal here is structure, not cleverness: each step is handled by
        /// a private method that can be evolved independently.
        /// </summary>
        public async Task<IReadOnlyList<NormalizedChunk>> BuildAsync(
            IndexFileContext context,
            CancellationToken cancellationToken = default)
        {
            ValidateContext(context);
            EnsureCatalogConfigured();

            // 7.1 Load the raw source text.
            var sourceText = await LoadSourceTextAsync(context, cancellationToken).ConfigureAwait(false);

            // 7.2 Split into symbol-level fragments.
            var symbolFragments = SplitIntoSymbolFragments(sourceText, context);

            // 7.3 Analyze each fragment (SubKind, primary type name, evidence).
            var analyzedSymbols = AnalyzeSymbolFragments(symbolFragments, context);

            // 7.4 (placeholder hook) Enrich with domain/model catalog.
            var enrichedSymbols = EnrichWithDomainModelCatalog(analyzedSymbols);

            // 7.5 Build Roslyn-based RagChunks for each symbol.
            var ragChunks = BuildRagChunksForSymbols(enrichedSymbols, context, cancellationToken);

            // 7.6 Normalize RagChunks into final embedding-ready chunks.
            var normalized = BuildNormalizedChunks(ragChunks, enrichedSymbols, context);

            return normalized;
        }

        #region 7.0 Validation helpers

        private static void ValidateContext(IndexFileContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (string.IsNullOrWhiteSpace(context.FullPath))
            {
                throw new ArgumentException("IndexFileContext.FullPath is required.", nameof(context));
            }

            if (string.IsNullOrWhiteSpace(context.RelativePath))
            {
                // We can tolerate this, but it is worth surfacing early.
                // Callers are expected to provide a relative path when possible.
            }
        }

        private void EnsureCatalogConfigured()
        {
            if (DomainModelCatalog == null)
            {
                throw new InvalidOperationException(
                    "DomainModelCatalog has not been set on DefaultNormalizedChunkBuilderService. " +
                    "The orchestrator must assign it before calling BuildAsync.");
            }
        }

        #endregion

        #region 7.1 Load source text

        private static async Task<string> LoadSourceTextAsync(
            IndexFileContext context,
            CancellationToken cancellationToken)
        {
            using (var stream = new FileStream(
                       context.FullPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       bufferSize: 4096,
                       useAsync: true))
            using (var reader = new StreamReader(stream))
            {
                var text = await reader.ReadToEndAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return text;
            }
        }

        #endregion

        #region 7.2 Split into symbol fragments

        /// <summary>
        /// Step 7.2
        /// Use <see cref="SymbolSplitter"/> to break the file into one
        /// fragment per primary type (class/record/struct/interface).
        ///
        /// Each <see cref="SymbolSplitResult"/> should contain a self-contained
        /// C# snippet (usings + namespace + type) and the relative path.
        /// </summary>
        private static IReadOnlyList<SymbolSplitResult> SplitIntoSymbolFragments(
            string sourceText,
            IndexFileContext context)
        {
            if (sourceText == null) throw new ArgumentNullException(nameof(sourceText));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var pathForAnalysis = context.RelativePath ?? context.FullPath;
            return SymbolSplitter.Split(sourceText, pathForAnalysis);
        }

        #endregion

        #region 7.3 Analyze symbol fragments

        /// <summary>
        /// Step 7.3
        /// For each symbol fragment, call <see cref="SourceKindAnalyzer"/> to
        /// determine SubKind, primary type name, etc.
        ///
        /// This step intentionally returns a small context object per symbol so
        /// that later steps (domain/model enrichment, chunk normalization) have
        /// all the information they need without re-analyzing the code.
        /// </summary>
        private static IReadOnlyList<SymbolProcessingContext> AnalyzeSymbolFragments(
            IReadOnlyList<SymbolSplitResult> fragments,
            IndexFileContext context)
        {
            if (fragments == null) throw new ArgumentNullException(nameof(fragments));

            var list = new List<SymbolProcessingContext>(fragments.Count);

            foreach (var fragment in fragments)
            {
                var kindResult = SourceKindAnalyzer.AnalyzeFile(
                    fragment.SourceText,
                    fragment.RelativePath);

                list.Add(new SymbolProcessingContext
                {
                    Fragment = fragment,
                    Kind = kindResult
                });
            }

            return list;
        }

        #endregion

        #region 7.4 Enrich with Domain/Model catalog (placeholder)

        /// <summary>
        /// Step 7.4 (to be fleshed out)
        ///
        /// Given the per-symbol analysis results, look up any relevant domain
        /// and model information from <see cref="DomainModelCatalog"/> and
        /// attach it to the processing context.
        ///
        /// For now this is a structural placeholder; it simply returns the
        /// input unchanged so we can evolve the catalog integration in a
        /// focused pass later.
        /// </summary>
        private IReadOnlyList<SymbolProcessingContext> EnrichWithDomainModelCatalog(
            IReadOnlyList<SymbolProcessingContext> analyzedSymbols)
        {
            if (analyzedSymbols == null) throw new ArgumentNullException(nameof(analyzedSymbols));

            // TODO: use DomainModelCatalog to populate DomainName, ModelName,
            // DomainTagline, ModelTagline on each SymbolProcessingContext.

            return analyzedSymbols;
        }

        #endregion

        #region 7.5 Build Roslyn RagChunks

        /// <summary>
        /// Step 7.5
        ///
        /// For each symbol fragment, call the Roslyn chunker via
        /// <see cref="IChunkerServices.ChunkCSharpWithRoslyn"/> to produce
        /// token-bounded <see cref="RagChunk"/> entries.
        ///
        /// At this stage the chunks are still code-centric and do not yet
        /// include rich natural language summaries.
        /// </summary>
        private IReadOnlyList<RagChunk> BuildRagChunksForSymbols(
            IReadOnlyList<SymbolProcessingContext> symbols,
            IndexFileContext context,
            CancellationToken cancellationToken)
        {
            if (symbols == null) throw new ArgumentNullException(nameof(symbols));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var chunks = new List<RagChunk>();
            var blobPath = context.RelativePath ?? context.FullPath;

            foreach (var symbol in symbols)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var plan = _chunkerServices.ChunkCSharpWithRoslyn(
                    symbol.Fragment.SourceText,
                    symbol.Fragment.RelativePath,
                    blobPath);

                if (plan?.Chunks == null || plan.Chunks.Count == 0)
                {
                    continue;
                }

                chunks.AddRange(plan.Chunks);
            }

            return chunks;
        }

        #endregion

        #region 7.6 Normalize into embedding-ready chunks

        /// <summary>
        /// Step 7.6
        ///
        /// Convert the Roslyn <see cref="RagChunk"/> objects plus the
        /// per-symbol processing context into final <see cref="NormalizedChunk"/>
        /// instances that will be embedded:
        ///
        ///  - Attach <see cref="DocumentIdentity"/>.
        ///  - Compute DocId.
        ///  - Copy symbol name/type and estimated token count.
        ///  - For now, use the raw RagChunk text as NormalizedText.
        ///
        /// Later, we will expand this step to prepend headers, include
        /// MethodSummaryBuilder output, and layer on domain/model summaries.
        /// </summary>
        private IReadOnlyList<NormalizedChunk> BuildNormalizedChunks(
            IReadOnlyList<RagChunk> ragChunks,
            IReadOnlyList<SymbolProcessingContext> symbols,
            IndexFileContext context)
        {
            if (ragChunks == null) throw new ArgumentNullException(nameof(ragChunks));
            if (symbols == null) throw new ArgumentNullException(nameof(symbols));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var result = new List<NormalizedChunk>(ragChunks.Count);

            foreach (var ragChunk in ragChunks)
            {
                var identity = new DocumentIdentity
                {
                    OrgId = context.OrgId,
                    ProjectId = context.ProjectId,
                    RepoId = context.RepoId,
                    RelativePath = context.RelativePath
                };
                identity.ComputeDocId();

                var normalized = new NormalizedChunk
                {
                    DocumentIdentity = identity,
                    NormalizedText = ragChunk.TextNormalized,
                    Summary = null, // will be populated when we add richer summaries
                    Symbol = ragChunk.Symbol,
                    SymbolType = ragChunk.SymbolType,
                    EstimatedTokens = ragChunk.EstimatedTokens
                };

                result.Add(normalized);
            }

            return result;
        }

        #endregion

        /// <summary>
        /// Internal carrier for all information we know about a symbol at this
        /// stage of the pipeline.
        ///
        /// This keeps the private method signatures small and makes it easy to
        /// layer on additional metadata later without changing each step.
        /// </summary>
        private sealed class SymbolProcessingContext
        {
            public SymbolSplitResult Fragment { get; set; }
            public SourceKindResult Kind { get; set; }

            // These will be populated when EnrichWithDomainModelCatalog is
            // implemented. For now they are left null.
            public string DomainName { get; set; }
            public string DomainTagline { get; set; }
            public string ModelName { get; set; }
            public string ModelTagline { get; set; }
        }
    }
}
