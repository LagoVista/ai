using LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.ContractPacks.Infrastructure.Services
{
    public class FileDiscoveryService : IFileDiscoveryService
    {

        private static readonly HashSet<string> BinaryExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico",
            ".pdf", ".zip", ".gz", ".tgz", ".7z", ".rar",
            ".dll", ".exe", ".so", ".dylib", ".a", ".lib",
            ".mp3", ".mp4", ".mov", ".wav", ".avi", ".mkv",
            ".woff", ".woff2", ".eot", ".ttf", ".otf"
        };

        public async Task<IReadOnlyList<DiscoveredFile>> DiscoverAsync(IngestionConfig config, string repoId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(repoId))
                throw new ArgumentNullException(nameof(repoId));

            if (config.Ingestion?.Repositories == null || !config.Ingestion.Repositories.Contains(repoId))
                throw new InvalidOperationException($"Repository '{repoId}' is not configured for ingestion.");

            var sourceRoot = config.Ingestion.SourceRoot;
            var include = config.Ingestion.Include;
            var exclude = config.Ingestion.Exclude;

            var repoRoot = Path.Combine(sourceRoot, repoId);

            var results = new List<DiscoveredFile>();

            if (!Directory.Exists(repoRoot))
                return results;
            var includeRegex = (include ?? new List<string>())
                .Select(g => ToRegex(g))
                .ToArray();

            var excludeRegex = (exclude ?? new List<string>())
                .Select(g => ToRegex(g))
                .ToArray();


            if (includeRegex.Length == 0)
                includeRegex = new[] { ToRegex("**/*") };

            foreach (var file in Directory.EnumerateFiles(repoRoot, "*", SearchOption.AllDirectories))
            {
                if (token.IsCancellationRequested)
                    break;

                var relative = Path.GetRelativePath(repoRoot, file);
                var normalized = Normalize(relative);

                if (!includeRegex.Any(rx => rx.IsMatch(normalized)))
                    continue;

                if (excludeRegex.Any(rx => rx.IsMatch(normalized)))
                    continue;

                var extension = Path.GetExtension(normalized);
                var isBinary = BinaryExtensions.Contains(extension);

                if (isBinary)
                    continue; // <- skip binary files entirely

                var info = new FileInfo(file);

                if (info.Length > 50 * 1024)
                    continue;

                results.Add(new DiscoveredFile
                {
                    RepoId = repoId,
                    FullPath = file,
                    RelativePath = normalized,
                    SizeBytes = info.Length,
                    IsBinary = false // since we skip binaries, everything here is non-binary
                });

            }

            return await Task.FromResult(results);
        }

        private static string Normalize(string path)
        {
            return path.Replace("\\", "/");
        }

        private static Regex ToRegex(string glob)
        {
            var g = Normalize(glob).TrimStart('/');
            var rx = Regex.Escape(g);

            rx = rx.Replace(@"\*\*/", @"(.+/)?");
            rx = rx.Replace(@"\*\*", @".*");
            rx = rx.Replace(@"\*", @"[^/]*");
            rx = rx.Replace(@"\?", @"[^/]");

            return new Regex("^" + rx + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
