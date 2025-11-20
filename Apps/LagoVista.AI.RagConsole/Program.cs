// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 84feeaeb64610697ce02a858eb217cf406b5821707963650d179e5c3e6374064
// IndexVersion: 2
// --- END CODE INDEX META ---
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.AI.Rag;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.Services;
using LagoVista.AI.Rag.Models;
using LagoVista.Core.Models;
using Newtonsoft.Json;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.RagConsole
{
    public static class Program
    {
        // Parse command-line arguments
        // Modes: "index" | "subkind" | "resources"
        static string mode = "index";
        static string repoId = null;
        static bool showHelp = false;

        public static async Task Main(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("/?", StringComparison.OrdinalIgnoreCase))
                {
                    showHelp = true;
                }
                else if (arg.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
                {
                    var value = arg.Substring("--mode=".Length);
                    if (value.Equals("index", StringComparison.OrdinalIgnoreCase))
                        mode = "index";
                    else if (value.Equals("subkind", StringComparison.OrdinalIgnoreCase) ||
                             value.Equals("subkind-test", StringComparison.OrdinalIgnoreCase))
                        mode = "subkind";
                    else if (value.Equals("resources", StringComparison.OrdinalIgnoreCase))
                        mode = "resources";
                    else
                    {
                        Console.WriteLine($"Unknown mode '{value}'.");
                        showHelp = true;
                    }
                }
                else if (arg.Equals("--mode", StringComparison.OrdinalIgnoreCase))
                {
                    showHelp = true;
                }
                else if (arg.StartsWith("--repo=", StringComparison.OrdinalIgnoreCase))
                {
                    repoId = arg.Substring("--repo=".Length);
                }
                else if (arg.Equals("--repo", StringComparison.OrdinalIgnoreCase))
                {
                    showHelp = true;
                }
                else
                {
                    Console.WriteLine($"Unknown argument: {arg}");
                    showHelp = true;
                }
            }

            if (showHelp)
            {
                PrintUsage();
                return;
            }

            // Load config
            var jsonConfig = System.IO.File.ReadAllText("appsettings.json");
            var cfg = JsonConvert.DeserializeObject<IngestionConfig>(jsonConfig)
                      ?? throw new InvalidOperationException("Failed to deserialize IngestionConfig from appsettings.json.");

            if (string.IsNullOrEmpty(cfg.Qdrant.ApiKey))
                cfg.Qdrant.ApiKey = Environment.GetEnvironmentVariable("QDRANT_API_KEY")
                                    ?? throw new ArgumentNullException("QDRANT_API_KEY");
            if (string.IsNullOrEmpty(cfg.Embeddings.ApiKey))
                cfg.Embeddings.ApiKey = Environment.GetEnvironmentVariable("EMBEDDING_API_KEY")
                                        ?? throw new ArgumentNullException("EMBEDDING_API_KEY");
            if (string.IsNullOrEmpty(cfg.ContentRepo.AccessKey))
                cfg.ContentRepo.AccessKey = Environment.GetEnvironmentVariable("PROD_TS_STORAGE_ACCOUNT_ACCESS_KEY")
                                            ?? throw new ArgumentNullException("PROD_TS_STORAGE_ACCOUNT_ACCESS_KEY");

            var vectoDb = new AgentContext()
            {
                VectorDatabaseCollectionName = cfg.Qdrant.Collection,
                VectorDatabaseApiKey = cfg.Qdrant.ApiKey,
                VectorDatabaseUri = cfg.Qdrant.Endpoint,
                LlmApiKey = cfg.Embeddings.ApiKey,
                AzureAccountId = cfg.ContentRepo.AccountId,
                AzureApiToken = cfg.ContentRepo.AccessKey,
                OwnerOrganization = new EntityHeader()
                {
                    Id = cfg.OrgId,
                }
            };

            if (mode.Equals("subkind", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Mode: subkind test");
                await RunSubKindTestAsync(cfg, repoId);
            }
            else if (mode.Equals("resources", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Mode: resources (RESX scan)");
                await RunResourceScanAsync(cfg, repoId);
            }
            else
            {
                Console.WriteLine("Mode: index");

                var repoToIndex = string.IsNullOrWhiteSpace(repoId) ? "backend" : repoId;

                Console.WriteLine($"Indexing repo: {repoToIndex}");
                var ingestor = new IngestorService(cfg, vectoDb);
                await ingestor.IngestAsync(repoToIndex);
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("LagoVista.AI.Rag Ingestor");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run                         # Default: index mode, repo 'backend'");
            Console.WriteLine("  dotnet run --mode=index            # Explicit index mode");
            Console.WriteLine("  dotnet run --mode=index --repo=MyRepo");
            Console.WriteLine("  dotnet run --mode=subkind          # SubKind test for all configured repos");
            Console.WriteLine("  dotnet run --mode=subkind --repo=MyRepo");
            Console.WriteLine("  dotnet run --mode=resources               # Print RESX labels for all repos");
            Console.WriteLine("  dotnet run --mode=resources --repo=MyRepo");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --mode=index       Run full indexing pipeline (current default behavior).");
            Console.WriteLine("  --mode=subkind     Run SubKindDetector test mode only; no vector DB writes.");
            Console.WriteLine("  --mode=resources   Scan *.resx files and print resource labels; no indexing.");
            Console.WriteLine("  --repo=<id>        Restrict to a single repository id from cfg.Ingestion.Repositories.");
            Console.WriteLine("  --help, -h, /?     Show this help.");
            Console.WriteLine();
            Console.WriteLine("Subkind test mode:");
            Console.WriteLine("  - Uses cfg.Ingestion.SourceRoot and cfg.Ingestion.Repositories");
            Console.WriteLine("  - Walks *.cs and *.resx files (respecting Include/Exclude via FileWalker)");
            Console.WriteLine("  - Calls SubKindDetector.DetectForFile on each file");
            Console.WriteLine("  - Prints per-file SubKind and a summary per repo");
            Console.WriteLine();
            Console.WriteLine("Resources mode:");
            Console.WriteLine("  - Uses cfg.Ingestion.SourceRoot and cfg.Ingestion.Repositories");
            Console.WriteLine("  - Walks *.resx files (respecting Include/Exclude via FileWalker)");
            Console.WriteLine("  - Uses ResxLabelScanner to read <data> entries");
            Console.WriteLine("  - Prints each resource file and its key/value pairs to the console");
            Console.WriteLine();
        }

        static async Task RunSubKindTestAsync(IngestionConfig cfg, string repoFilter)
        {
            if (cfg.Ingestion == null)
            {
                Console.WriteLine("Ingestion section is missing from config.");
                return;
            }

            if (string.IsNullOrWhiteSpace(cfg.Ingestion.SourceRoot))
            {
                Console.WriteLine("Ingestion.SourceRoot is not configured.");
                return;
            }

            var sourceRoot = cfg.Ingestion.SourceRoot;
            if (!Directory.Exists(sourceRoot))
            {
                Console.WriteLine($"SourceRoot does not exist: {sourceRoot}");
                return;
            }

            var repos = cfg.Ingestion.Repositories ?? new List<string>();
            if (repos.Count == 0)
            {
                Console.WriteLine("No repositories configured in cfg.Ingestion.Repositories.");
                return;
            }

            var targetRepos = string.IsNullOrWhiteSpace(repoFilter)
                ? repos
                : repos.Where(r => string.Equals(r, repoFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (targetRepos.Count == 0)
            {
                Console.WriteLine($"No matching repository found for filter '{repoFilter}'.");
                return;
            }

            foreach (var repoId in targetRepos)
            {
                var repoRoot = Path.Combine(sourceRoot, repoId);
                if (!Directory.Exists(repoRoot))
                {
                    Console.WriteLine($"[SubKindTest] Repo '{repoId}': directory not found at '{repoRoot}'. Skipping.");
                    continue;
                }

                var include = cfg.Ingestion.Include ?? new List<string>();
                var exclude = cfg.Ingestion.Exclude ?? new List<string>();

                var allFiles = FileWalker
                    .EnumerateFiles(repoRoot, include, exclude)
                    .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                Console.WriteLine();
                Console.WriteLine($"[SubKindTest] Repo '{repoId}': {allFiles.Count} files found.");

                var byKind = new Dictionary<CodeSubKind, int>();
                int fileIndex = 1;

                var results = new List<SubKindDetectionResult>();

                foreach (var fullPath in allFiles)
                {
                    var text = await System.IO.File.ReadAllTextAsync(fullPath);
                    var relPath = Path.GetRelativePath(repoRoot, fullPath).Replace('\\', '/');

                    var detectionResults = SubKindDetector.DetectForFile(text, relPath);

                    foreach (var result in detectionResults)
                    {
                        if (!byKind.TryGetValue(result.SubKind, out var count))
                            count = 0;
                        byKind[result.SubKind] = count + 1;
                        results.Add(result);

                        Console.WriteLine($"  {fileIndex,4}/{allFiles.Count,4}  [{result.SubKind}] {relPath}  ({result.PrimaryTypeName})");
                        fileIndex++;
                    }
                }

                Console.WriteLine("----\r\n");

                foreach (var result in results.Where(r => r.SubKind == CodeSubKind.Other))
                {
                    Console.WriteLine($"[WARNING] Other SubKind detected in file '{result.Path}' - Type '{result.PrimaryTypeName}'");
                }

                Console.WriteLine();
                Console.WriteLine($"[SubKindTest] Repo '{repoId}' summary:");
                foreach (var kvp in byKind.OrderBy(k => k.Key))
                {
                    Console.WriteLine($"  {kvp.Key,-18}: {kvp.Value} occurrence(s)");
                }

                Console.WriteLine();
            }
        }

        static Task RunResourceScanAsync(IngestionConfig cfg, string repoFilter)
        {
            if (cfg.Ingestion == null)
            {
                Console.WriteLine("Ingestion section is missing from config.");
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(cfg.Ingestion.SourceRoot))
            {
                Console.WriteLine("Ingestion.SourceRoot is not configured.");
                return Task.CompletedTask;
            }

            var sourceRoot = cfg.Ingestion.SourceRoot;
            if (!Directory.Exists(sourceRoot))
            {
                Console.WriteLine($"SourceRoot does not exist: {sourceRoot}");
                return Task.CompletedTask;
            }

            var repos = cfg.Ingestion.Repositories ?? new List<string>();
            if (repos.Count == 0)
            {
                Console.WriteLine("No repositories configured in cfg.Ingestion.Repositories.");
                return Task.CompletedTask;
            }

            var targetRepos = string.IsNullOrWhiteSpace(repoFilter)
                ? repos
                : repos.Where(r => string.Equals(r, repoFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (targetRepos.Count == 0)
            {
                Console.WriteLine($"No matching repository found for filter '{repoFilter}'.");
                return Task.CompletedTask;
            }

            foreach (var repoId in targetRepos)
            {
                var repoRoot = Path.Combine(sourceRoot, repoId);
                if (!Directory.Exists(repoRoot))
                {
                    Console.WriteLine($"[RESX] Repo '{repoId}': directory not found at '{repoRoot}'. Skipping.");
                    continue;
                }

                Console.WriteLine();
                Console.WriteLine($"[RESX] Repo '{repoId}'");

                var maps = ResxLabelScanner.ScanResxTree(repoRoot);

                if (maps.Count == 0)
                {
                    Console.WriteLine("  (no .resx files found)");
                    continue;
                }

                foreach (var fileEntry in maps.OrderBy(k => k.Key))
                {
                    Console.WriteLine();
                    Console.WriteLine($"  File: {fileEntry.Key}");

                    foreach (var kvp in fileEntry.Value.OrderBy(k => k.Key))
                    {
                        Console.WriteLine($"    {kvp.Key} = {kvp.Value}");
                    }
                }

                Console.WriteLine();
            }

            return Task.CompletedTask;
        }
    }
}
