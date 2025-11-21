using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Services
{
    /// <summary>
    /// Simple file system based implementation of IFileDiscoveryService.
    ///
    /// The repoRoot parameter is treated as the on-disk root directory to
    /// scan. Include/exclude rules can be layered on top in future iterations.
    /// </summary>
    public sealed class FileSystemDiscoveryService : IFileDiscoveryService
    {
        public Task<IReadOnlyList<DiscoveredFile>> DiscoverAsync(string repoRoot, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(repoRoot))
                throw new ArgumentNullException(nameof(repoRoot));

            var results = new List<DiscoveredFile>();

            if (!Directory.Exists(repoRoot))
                return Task.FromResult<IReadOnlyList<DiscoveredFile>>(results);

            foreach (var file in Directory.EnumerateFiles(repoRoot, "*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();

                var relativePath = GetRelativePath(repoRoot, file);
                var info = new FileInfo(file);

                results.Add(new DiscoveredFile
                {
                    RepoId = repoRoot,
                    FullPath = file,
                    RelativePath = relativePath,
                    SizeBytes = info.Length,
                    IsBinary = IsLikelyBinary(file)
                });
            }

            return Task.FromResult<IReadOnlyList<DiscoveredFile>>(results);
        }

        private static string GetRelativePath(string root, string fullPath)
        {
            var rel = fullPath.Substring(root.Length).TrimStart('\\', '/');
            return rel.Replace('\\', '/');
        }

        private static bool IsLikelyBinary(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) return false;

            // Very basic heuristic; this can be refined later.
            var binaryExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".dll", ".exe", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".zip", ".tar", ".gz"
            };

            return binaryExtensions.Contains(ext);
        }
    }
}
