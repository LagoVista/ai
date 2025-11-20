using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Result of running SubKind detection over a single C# file.
    /// Kept simple and DTO-style so the console or other tools can format it as they like.
    /// </summary>
    public sealed class SubKindTestResult
    {
        public string RepoId { get; set; }
        public string RepoRelativePath { get; set; }

        public CodeSubKind SubKind { get; set; }
        public string SubKindString => SubKind.ToString();

        public string PrimaryTypeName { get; set; }
    }

    /// <summary>
    /// Library entry point for "SubKind test mode".
    /// Uses the same IngestionConfig (SourceRoot, Repositories, Include/Exclude)
    /// and your SubKindDetector, but does NOT touch Qdrant, local index, or pipelines.
    /// </summary>
    public static class SubKindTestRunner
    {
        public static async Task<IReadOnlyList<SubKindTestResult>> RunAsync(
            IngestionConfig config,
            string repoFilter,
            CancellationToken cancellationToken)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.Ingestion == null)
                throw new InvalidOperationException("Ingestion section is missing from config.");
            if (string.IsNullOrWhiteSpace(config.Ingestion.SourceRoot))
                throw new InvalidOperationException("Ingestion.SourceRoot is not configured.");

            var sourceRoot = config.Ingestion.SourceRoot;
            if (!Directory.Exists(sourceRoot))
                throw new DirectoryNotFoundException($"SourceRoot does not exist: {sourceRoot}");

            var repos = config.Ingestion.Repositories ?? new List<string>();
            if (repos.Count == 0)
                throw new InvalidOperationException("No repositories configured in Ingestion.Repositories.");

            var targetRepos = string.IsNullOrWhiteSpace(repoFilter)
                ? repos
                : repos.Where(r => string.Equals(r, repoFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (targetRepos.Count == 0)
                throw new InvalidOperationException($"No matching repository found for filter '{repoFilter}'.");

            var include = config.Ingestion.Include ?? new List<string>();
            var exclude = config.Ingestion.Exclude ?? new List<string>();

            var results = new List<SubKindTestResult>();

            foreach (var repoId in targetRepos)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var repoRoot = Path.Combine(sourceRoot, repoId);
                if (!Directory.Exists(repoRoot))
                {
                    // Silent skip; caller can decide if they want to warn the user.
                    continue;
                }

                // Reuse the same file discovery rules as IndexRunOrchestrator/FileWalker.
                var files = FileWalker
                    .EnumerateFiles(repoRoot, include, exclude)
                    .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var fullPath in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var text = await File.ReadAllTextAsync(fullPath, cancellationToken);
                    var repoRelative = Path.GetRelativePath(repoRoot, fullPath)
                        .Replace('\\', '/');

                    var detection = SubKindDetector.DetectForFile(text, repoRelative);

                    results.Add(new SubKindTestResult
                    {
                        RepoId = repoId,
                        RepoRelativePath = repoRelative,
                        SubKind = detection.SubKind,
                        PrimaryTypeName = detection.PrimaryTypeName
                    });
                }
            }

            return results;
        }
    }
}
