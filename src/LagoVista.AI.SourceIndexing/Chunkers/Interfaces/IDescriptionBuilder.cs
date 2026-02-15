using System.Threading.Tasks;
using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Rag.Chunkers.Interfaces
{
    /// <summary>
    /// IDX-069 description builder contract. Each builder converts a single symbol or
    /// document segment into a fully-enriched IRagDescription instance.
    /// </summary>
    public interface IDescriptionBuilder
    {
        /// <summary>
        /// Short, human-readable identifier used in logs, UIs, and agent reasoning.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Brief explanation of what the builder handles and how it interprets symbols.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Core build pipeline entry point. Converts a symbol or segment plus context into
        /// a concrete IRagDescription. Expected conditions should be represented using
        /// InvokeResult rather than thrown exceptions.
        /// </summary>
        /// <param name="fileContext">Normalized file context for the segment being processed.</param>
        /// <param name="symbolText">Raw text for the symbol or document segment.</param>
        /// <param name="domainCatalogService">Domain catalog for enrichment.</param>
        /// <param name="resourceDictionary">RESX-backed resources used for localization and labels.</param>
        Task<InvokeResult<IRagDescription>> BuildAsync(
            IndexFileContext fileContext,
            string symbolText,
            IDomainCatalogService domainCatalogService,
            IResourceDictionary resourceDictionary);
    }
}
