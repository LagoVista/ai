using System;
using LagoVista.AI.Rag.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.Chunkers.Interfaces;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// IDX-072 registration helper for domain description builder.
    /// </summary>
    public static class DomainDescriptionBuilderRegistryExtensions
    {
        /// <summary>
        /// Registers the DomainDescriptionBuilder for SubtypeKind.DomainDescription.
        /// Call this during startup after constructing the registry.
        /// </summary>
        public static void RegisterDomainDescriptionBuilder(this IDescriptionBuilderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            registry.Register<DomainDescriptionBuilder>(SubtypeKind.DomainDescription);
        }
    }
}
