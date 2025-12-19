using System.Collections.Generic;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.Chunkers.Interfaces
{
    /// <summary>
    /// Registry for mapping SubtypeKind values to one or more description builders.
    /// Responsible only for grouping by SubtypeKind and resolving builder instances;
    /// applicability and flavor decisions live inside individual builders.
    /// </summary>
    public interface IDescriptionBuilderRegistry
    {
        /// <summary>
        /// Returns all builders registered for the specified SubtypeKind in a deterministic order.
        /// May return an empty list if no builders are registered for the kind.
        /// </summary>
        IReadOnlyList<IDescriptionBuilder> GetBuilders(SubtypeKind subtypeKind);

        /// <summary>
        /// Registers a builder type for the specified SubtypeKind. The registry must prevent
        /// exact duplicates of the pair (builder type, SubtypeKind) but may allow the same
        /// builder type to be registered for multiple different SubtypeKind values.
        /// </summary>
        void Register<TBuilder>(SubtypeKind subtypeKind) where TBuilder : IDescriptionBuilder;
    }
}
