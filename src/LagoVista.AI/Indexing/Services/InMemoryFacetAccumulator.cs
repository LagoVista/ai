using System;
using System.Collections.Generic;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;

namespace LagoVista.AI.Indexing.Services
{
    /// <summary>
    /// In-memory implementation of IFacetAccumulator.
    ///
    /// Maintains a de-duplicated set of FacetValue entries for the lifetime
    /// of an indexing run. Uniqueness is enforced on the combination of
    /// Type, Value, ParentType, and ParentValue.
    /// </summary>
    public sealed class InMemoryFacetAccumulator : IFacetAccumulator
    {
        private readonly HashSet<FacetValue> _facets;

        public InMemoryFacetAccumulator()
        {
            _facets = new HashSet<FacetValue>(new FacetValueComparer());
        }

        public void AddFacet(FacetValue facet)
        {
            if (facet == null)
                return;

            _facets.Add(Clone(facet));
        }

        public void AddFacets(IEnumerable<FacetValue> facets)
        {
            if (facets == null)
                return;

            foreach (var facet in facets)
            {
                if (facet == null) continue;
                _facets.Add(Clone(facet));
            }
        }

        public IReadOnlyList<FacetValue> GetAll()
        {
            return new List<FacetValue>(_facets);
        }

        public void Clear()
        {
            _facets.Clear();
        }

        private static FacetValue Clone(FacetValue source)
        {
            if (source == null) return null;

            return new FacetValue
            {
                Type = source.Type,
                Value = source.Value,
                ParentType = source.ParentType,
                ParentValue = source.ParentValue
            };
        }

        private sealed class FacetValueComparer : IEqualityComparer<FacetValue>
        {
            public bool Equals(FacetValue x, FacetValue y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x == null || y == null) return false;

                return string.Equals(x.Type, y.Type, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.ParentType, y.ParentType, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.ParentValue, y.ParentValue, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(FacetValue obj)
            {
                if (obj == null) return 0;

                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + (obj.Type ?? string.Empty).ToLowerInvariant().GetHashCode();
                    hash = hash * 23 + (obj.Value ?? string.Empty).ToLowerInvariant().GetHashCode();
                    hash = hash * 23 + (obj.ParentType ?? string.Empty).ToLowerInvariant().GetHashCode();
                    hash = hash * 23 + (obj.ParentValue ?? string.Empty).ToLowerInvariant().GetHashCode();
                    return hash;
                }
            }
        }
    }
}
