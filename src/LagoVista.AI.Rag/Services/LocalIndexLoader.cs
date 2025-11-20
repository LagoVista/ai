using System;
using System.IO;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Helper for loading the local index store for a given repository root.
    ///
    /// This centralizes the path logic so callers don't need to know where
    /// local-index.json lives on disk.
    ///
    /// Implements IDX-0036:
    ///   &lt;repoRoot&gt;/.nuvos/index/local-index.json
    /// </summary>
    public static class LocalIndexLoader
    {
        /// <summary>
        /// Load the LocalIndexStore for a given repository root.
        /// If the index file does not exist, an empty store is returned.
        /// If the index file is corrupt, it is renamed with a .corrupt-* suffix
        /// and an empty store is returned.
        /// </summary>
        /// <param name="repoRoot">The root directory of the repository on disk.</param>
        public static LocalIndexStore LoadForRepo(string repoRoot)
        {
            if (string.IsNullOrWhiteSpace(repoRoot))
                throw new ArgumentNullException(nameof(repoRoot));

            if (!Directory.Exists(repoRoot))
                throw new DirectoryNotFoundException($"Repository root not found: {repoRoot}");

            var indexPath = LocalIndexStore.GetDefaultIndexPath(repoRoot);
            return LocalIndexStore.Load(indexPath);
        }
    }
}
