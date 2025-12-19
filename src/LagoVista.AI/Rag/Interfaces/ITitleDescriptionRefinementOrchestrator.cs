using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Interfaces
{
    public interface ITitleDescriptionRefinementOrchestrator
    {
        Task RunAsync(
           IReadOnlyList<DiscoveredFile> files,
           IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> resources,
           CancellationToken cancellationToken);
    }
}
