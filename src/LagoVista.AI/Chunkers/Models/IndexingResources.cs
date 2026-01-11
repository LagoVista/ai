using System;
using System.Collections.Generic;

namespace LagoVista.AI.Indexing.Models
{
    /// <summary>
    /// Read-only lookup/reference data for indexing. No services should be placed here.
    /// </summary>
    public class IndexingResources
    {
        public IDictionary<string, object> Items { get; } =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }
}
