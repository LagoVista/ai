using LagoVista.AI.Indexing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Quality.Interfaces
{
    public interface ITitleDescriptionRefinementOrchestrator
    {
        Task RunAsync(
           IReadOnlyList<DiscoveredFile> files,
           IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> resources,
           CancellationToken cancellationToken);
    }
}
