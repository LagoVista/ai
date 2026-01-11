using System.Collections.Generic;
using LagoVista.AI.Chunkers.Providers.DomainDescription;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Rag.Chunkers.Interfaces
{
    /// <summary>
    /// File-level chunking and summary service.
    ///
    /// Given a single source file (and optional resources + domain/model
    /// catalog), produces the semantic summary sections that will be
    /// normalized and sent to the embedder.
    /// </summary>
    public interface IFileChunkingService
    {
        /// <summary>
        /// Build all summary sections for a single source file.
        ///
        /// Implementations are expected to:
        ///  - Parse the source text (e.g. via Roslyn).
        ///  - Identify models, managers, domains, etc.
        ///  - Use DomainModelCatalog (if provided) to enrich titles/taglines.
        ///  - Produce normalized SummarySection entries ready for embedding.
        /// </summary>
        /// <param name="sourceText">Raw source code for the file.</param>
        /// <param name="relativePath">File path relative to the repo root.</param>
        /// <param name="resources">Optional resource dictionaries keyed by resource id.</param>
        /// <param name="catalog">Optional domain/model catalog built during the pre-scan phase.</param>
        /// <returns>A read-only list of summary sections for this file.</returns>
        IReadOnlyList<SummarySection> BuildSummarySectionsForFile(
            string sourceText,
            string relativePath,
            IReadOnlyDictionary<string, string> resources,
            DomainModelCatalog catalog = null);
    }
}
