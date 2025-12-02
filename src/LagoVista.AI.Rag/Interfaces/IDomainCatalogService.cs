using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models; // DiscoveredFile
using LagoVista.AI.Rag.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Contract for the Domain Catalog Service (IDX-071).
    /// Provides an immutable snapshot of domains and interesting model classes
    /// and basic query operations over that snapshot.
    /// </summary>
    public interface IDomainCatalogService
    {
        /// <summary>
        /// Build or refresh the domain catalog from the provided C# files and resource map,
        /// then persist it to the canonical location.
        /// </summary>
        /// <param name="files">
        /// List of discovered C# files to scan. Only .cs files under the src root are
        /// considered; files under tests/... are ignored.
        /// </param>
        /// <param name="resources">Resource key/value map used for descriptions.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<InvokeResult> BuildCatalogAsync(
            IReadOnlyList<DiscoveredFile> files,
            IReadOnlyDictionary<string, string> resources,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Load an existing catalog from disk into memory.
        /// </summary>
        Task<InvokeResult> LoadCatalogAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns all domains in the current in-memory catalog.
        /// Returns an empty list if no catalog is loaded or if there are no domains.
        /// </summary>
        IReadOnlyList<DomainEntry> GetAllDomains();

        /// <summary>
        /// Returns all model classes for the given domain key (case-insensitive).
        /// Returns an empty list if the domain does not exist or has no classes.
        /// </summary>
        IReadOnlyList<ModelClassEntry> GetClassesForDomain(string domainKey);

        /// <summary>
        /// Resolve the owning domain for the given class name (simple or fully-qualified).
        /// Returns an InvokeResult that may represent:
        ///  - Success with a DomainEntry
        ///  - A non-fatal "not found" state
        ///  - A fatal internal error
        /// </summary>
        InvokeResult<DomainEntry> FindDomainForClass(string className);
    }
}
