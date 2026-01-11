using System;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Chunkers.Providers.DomainDescription;
using LagoVista.AI.Chunkers.Providers.Endpoints;

namespace LagoVista.AI.Indexing.Services
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
            registry.Register<EndpointDescriptionBuilder>(SubtypeKind.Controller);
        }
    }
}
