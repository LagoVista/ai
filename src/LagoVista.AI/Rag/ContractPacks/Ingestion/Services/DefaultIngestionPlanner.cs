using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Services
{
    /// <summary>
    /// Default implementation of IFileIngestionPlanner.
    ///
    /// Compares the discovered file set to the current LocalIndexStore and
    /// produces a FileIngestionPlan:
    ///  - New files => FilesToIndex with Reindex = "full".
    ///  - Existing files => FilesToIndex with Reindex = "chunk" if hashes differ
    ///    (hash computation to be integrated in a future pass).
    ///  - Missing files => DocsToDelete.
    /// </summary>
    public sealed class DefaultIngestionPlanner : IFileIngestionPlanner
    {
        public Task<FileIngestionPlan> BuildPlanAsync(
            string repoId,
            IReadOnlyList<string> discoveredRelativePaths,
            LocalIndexStore localIndex,
            CancellationToken token = default)
        {
            if (discoveredRelativePaths == null)
                throw new ArgumentNullException(nameof(discoveredRelativePaths));
            if (localIndex == null)
                throw new ArgumentNullException(nameof(localIndex));

            var plan = new FileIngestionPlan
            {
                RepoRoot = repoId,
                RepoUrl = null,
                BranchRef = null,
                ProjectId = localIndex.ProjectRoot
            };

            var discoveredSet = new HashSet<string>(discoveredRelativePaths, StringComparer.OrdinalIgnoreCase);
            var existingRecords = localIndex.GetAll().ToList();
            var existingPaths = new HashSet<string>(existingRecords.Select(r => r.FilePath), StringComparer.OrdinalIgnoreCase);

            // New or changed files
            foreach (var path in discoveredSet)
            {
                token.ThrowIfCancellationRequested();

                var record = existingRecords.FirstOrDefault(r => string.Equals(r.FilePath, path, StringComparison.OrdinalIgnoreCase));
                if (record == null)
                {
                    // Brand new file: full index
                    plan.FilesToIndex.Add(new PlannedFileIngestion
                    {
                        RepoRoot = repoId,
                        CanonicalPath = path,
                        DocId = null,
                        Reindex = "full",
                        IsActive = true
                    });
                }
                else
                {
                    // Existing file: for now, we conservatively treat it as needing a chunk-level reindex
                    plan.FilesToIndex.Add(new PlannedFileIngestion
                    {
                        RepoRoot = repoId,
                        CanonicalPath = path,
                        DocId = record.DocId,
                        Reindex = string.IsNullOrWhiteSpace(record.Reindex) ? "chunk" : record.Reindex,
                        IsActive = record.IsActive
                    });
                }
            }

            // Missing files => DocsToDelete
            foreach (var record in existingRecords)
            {
                token.ThrowIfCancellationRequested();

                if (!discoveredSet.Contains(record.FilePath))
                {
                    plan.DocsToDelete.Add(new PlannedDocDeletion
                    {
                        RepoRoot = repoId,
                        CanonicalPath = record.FilePath,
                        DocId = record.DocId,
                        ProjectId = plan.ProjectId,
                        RepoUrl = plan.RepoUrl
                    });
                }
            }

            return Task.FromResult(plan);
        }
    }
}
