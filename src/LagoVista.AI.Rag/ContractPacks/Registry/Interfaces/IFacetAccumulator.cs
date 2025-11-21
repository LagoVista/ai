using System.Collections.Generic;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.ContractPacks.Registry.Interfaces
{
    /// <summary>
    /// Accumulates and de-duplicates facet values discovered during an
    /// indexing run. This is the in-memory staging area that ultimately
    /// feeds IMetadataRegistryClient.
    /// </summary>
    public interface IFacetAccumulator
    {
        /// <summary>
        /// Add a single facet value to the accumulator. Implementations
        /// should de-duplicate based on the facet's type/value/parent fields.
        /// </summary>
        void AddFacet(FacetValue facet);

        /// <summary>
        /// Add multiple facet values in a batch. Implementations should
        /// still enforce de-duplication semantics.
        /// </summary>
        void AddFacets(IEnumerable<FacetValue> facets);

        /// <summary>
        /// Get the current set of unique facet values accumulated so far.
        /// </summary>
        IReadOnlyList<FacetValue> GetAll();

        /// <summary>
        /// Clear all accumulated facet values.
        /// </summary>
        void Clear();
    }
}
