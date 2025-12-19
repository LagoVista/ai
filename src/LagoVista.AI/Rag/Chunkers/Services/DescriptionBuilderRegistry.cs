using System;
using System.Collections.Generic;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// Default IDX-069 description builder registry implementation. Stores builder types
    /// grouped by SubtypeKind and resolves instances from the DI container at runtime.
    /// </summary>
    public class DescriptionBuilderRegistry : IDescriptionBuilderRegistry
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<SubtypeKind, List<Type>> _builderTypes = new Dictionary<SubtypeKind, List<Type>>();

        public DescriptionBuilderRegistry(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <inheritdoc />
        public IReadOnlyList<IDescriptionBuilder> GetBuilders(SubtypeKind subtypeKind)
        {
            if (!_builderTypes.TryGetValue(subtypeKind, out var types) || types.Count == 0)
            {
                return Array.Empty<IDescriptionBuilder>();
            }

            var builders = new List<IDescriptionBuilder>(types.Count);

            foreach (var type in types)
            {
                if (_serviceProvider.GetService(type) is IDescriptionBuilder builder)
                {
                    builders.Add(builder);
                }
            }

            return builders;
        }

        /// <inheritdoc />
        public void Register<TBuilder>(SubtypeKind subtypeKind) where TBuilder : IDescriptionBuilder
        {
            var type = typeof(TBuilder);

            if (!_builderTypes.TryGetValue(subtypeKind, out var list))
            {
                list = new List<Type>();
                _builderTypes[subtypeKind] = list;
            }

            // Prevent exact duplicates for (builder type, SubtypeKind).
            if (list.Contains(type))
            {
                return;
            }

            list.Add(type);
        }
    }
}
