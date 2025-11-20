using System;
using System.Collections.Generic;
using System.Linq;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Builds an IngestionPlan from discovered files and a LocalIndexStore.
    /// This is a pure planner; it does not talk to Qdrant or the LLM.
    /// </summary>
    public class IngestionPlanner
    {
        private readonly LocalIndexStore _indexStore;
        private readonly bool _reindexAll;

        /// <summary>
        /// reindexAll corresponds to config.Ingestion.Reindex (string) when equal to "true" (case-insensitive)
        /// or to a higher-level "force reindex" switch.
        /// </summary>
        public IngestionPlanner(LocalIndexStore indexStore, bool reindexAll)
        {
            _indexStore = indexStore ?? throw new ArgumentNullException(nameof(indexStore));
            _reindexAll = reindexAll;
        }

        /// <summary>
        /// Build an ingestion plan for the given set of discovered file paths.
        /// File paths should use the same canonical form that LocalIndexStore expects.
        /// </summary>
        public IngestionPlan BuildPlan(IEnumerable<string> discoveredFilePaths)
        {
            if (discoveredFilePaths == null) throw new ArgumentNullException(nameof(discoveredFilePaths));

            var current = new HashSet<string>(discoveredFilePaths, StringComparer.OrdinalIgnoreCase);

            var planned = new List<PlannedFile>();

            foreach (var filePath in current.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var record = _indexStore.GetOrAdd(filePath);

                bool forceReindex = string.Equals(record.Reindex, "chunk", StringComparison.OrdinalIgnoreCase) || string.Equals(record.Reindex, "full", StringComparison.OrdinalIgnoreCase);

                bool needsIndex = _reindexAll || forceReindex || string.IsNullOrWhiteSpace(record.ContentHash) || string.IsNullOrWhiteSpace(record.ActiveContentHash) || !string.Equals(record.ContentHash, record.ActiveContentHash, StringComparison.OrdinalIgnoreCase);

                planned.Add(new PlannedFile { FilePath = filePath, Record = record, NeedsIndex = needsIndex, IsMissing = false, ForceReindex = forceReindex });
            }

            foreach (var missing in _indexStore.GetMissingFiles(current))
            {
                planned.Add(new PlannedFile { FilePath = missing.FilePath, Record = missing, NeedsIndex = false, IsMissing = true, ForceReindex = false });
            }

            return new IngestionPlan(planned);
        }
    }
}
