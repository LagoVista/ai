using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Thin coordinator around LocalIndexStore for a single repository root.
    ///
    /// Responsibilities:
    ///  - Load local-index.json for the repo.
    ///  - Compute ActiveContentHash for discovered files.
    ///  - Expose helpers to get missing files and files needing reindex.
    ///
    /// This is a building block we will plug into the next revision of Ingestor
    /// to honor IDX-0034 / IDX-0035 / IDX-0036.
    /// </summary>
    public class LocalIndexManager
    {
        private readonly string _repoRoot;
        private readonly LocalIndexStore _store;

        public LocalIndexManager(string repoRoot)
        {
            if (string.IsNullOrWhiteSpace(repoRoot))
                throw new ArgumentNullException(nameof(repoRoot));

            _repoRoot = Path.GetFullPath(repoRoot);
            var indexPath = LocalIndexStore.GetDefaultIndexPath(_repoRoot);
            _store = LocalIndexStore.Load(indexPath);
        }

        /// <summary>
        /// Underlying LocalIndexStore, for advanced scenarios.
        /// </summary>
        public LocalIndexStore Store => _store;

        /// <summary>
        /// Compute ActiveContentHash for each file and update the store.
        /// filePaths should be a stable, canonical representation (we typically
        /// use full paths rooted at the repo directory).
        /// </summary>
        public void RefreshActiveContent(IEnumerable<string> filePaths)
        {
            if (filePaths == null) return;

            foreach (var file in filePaths)
            {
                if (string.IsNullOrWhiteSpace(file))
                    continue;

                if (!File.Exists(file))
                    continue;

                var hash = ContentHashHelper.ComputeFileHash(file);
                _store.UpdateActiveContentHash(file, hash);
            }
        }

        /// <summary>
        /// Records whose FilePath no longer exists on disk.
        /// Caller will use this to perform stale-chunk deletion per IDX-0035.
        /// </summary>
        public IEnumerable<LocalIndexRecord> GetMissingFiles(IEnumerable<string> currentFilePaths)
        {
            return _store.GetMissingFiles(currentFilePaths ?? Array.Empty<string>());
        }

        /// <summary>
        /// Returns the set of files that should be (re)indexed for this run
        /// based on ContentHash vs ActiveContentHash and Reindex flag.
        ///
        /// Current rules:
        ///   - No ContentHash yet => new file => index.
        ///   - Reindex == "chunk" or "full" => index.
        ///   - IsActive (hash mismatch) => index.
        /// </summary>
        public IEnumerable<string> GetFilesNeedingIndex()
        {
            return _store.Records
                .Where(r =>
                    string.IsNullOrWhiteSpace(r.ContentHash) ||
                    string.Equals(r.Reindex, "chunk", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r.Reindex, "full", StringComparison.OrdinalIgnoreCase) ||
                    r.IsActive)
                .Select(r => r.FilePath);
        }

        /// <summary>
        /// Persist the local index to disk using the store's atomic save.
        /// </summary>
        public void Save()
        {
            _store.Save();
        }
    }
}
