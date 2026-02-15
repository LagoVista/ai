using LagoVista.AI.Rag.Chunkers.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace LagoVista.AI.Interfaces
{
    public interface IResourceUsageTableWriter
    {
        Task ReplaceUsagesForFileAsync(IEnumerable<ResourceUsageRecord> records, CancellationToken token = default);
    }
}
