using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Indexing.Models;

namespace LagoVista.AI.FileServices.Indexes
{
    /// <summary>
    /// Responsibility: given the current LocalIndex and the set of discovered
    /// files, compute a plan that indicates which files should be indexed and
    /// which documents should be deleted.
    /// </summary>
    public interface IFileIngestionPlanner
    {
        Task<FileIngestionPlan> BuildPlanAsync(
            string repoId,
            IReadOnlyList<string> discoveredRelativePaths,
            LocalIndexStore localIndex,
            CancellationToken token = default);
    }
}
