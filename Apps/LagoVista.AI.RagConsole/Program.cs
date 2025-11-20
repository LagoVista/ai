// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 84feeaeb64610697ce02a858eb217cf406b5821707963650d179e5c3e6374064
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.AI.Rag;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.Services;
using LagoVista.AI.Rag.Models;
using LagoVista.Core.Models;
using Newtonsoft.Json;

// Parse command-line arguments
var mode = "index";          // "index" | "subkind"
string repoId = null;
bool showHelp = false;

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
        else
        {
            Console.WriteLine($"Unknown mode '{value}'.");
            showHelp = true;
        }
    }
    else if (arg.Equals("--mode", StringComparison.OrdinalIgnoreCase))
    {
        // consume next token if present
        // (top-level statements can't easily peek, so just ignore here;
        //  users should prefer --mode=subkind form)
        showHelp = true;
    }
    else if (arg.StartsWith("--repo=", StringComparison.OrdinalIgnoreCase))
    {
        repoId = arg.Substring("--repo=".Length);
    }
    else if (arg.Equals("--repo", StringComparison.OrdinalIgnoreCase))
    {
        // same note as --mode: require --repo=MyRepo for now
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

if (String.IsNullOrEmpty(cfg.Qdrant.ApiKey))
    cfg.Qdrant.ApiKey = Environment.GetEnvironmentVariable("QDRANT_API_KEY")
                        ?? throw new ArgumentNullException("QDRANT_API_KEY");
if (String.IsNullOrEmpty(cfg.Embeddings.ApiKey))
    cfg.Embeddings.ApiKey = Environment.GetEnvironmentVariable("EMBEDDING_API_KEY")
                            ?? throw new ArgumentNullException("EMBEDDING_API_KEY");
if (String.IsNullOrEmpty(cfg.ContentRepo.AccessKey))
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
else
{
    Console.WriteLine("Mode: index");

    // Default repo id is "backend" to preserve your existing behavior
    var repoToIndex = string.IsNullOrWhiteSpace(repoId) ? "backend" : repoId;

    Console.WriteLine($"Indexing repo: {repoToIndex}");
    var ingestor = new Ingestor(cfg, vectoDb);
    await ingestor.IngestAsync(repoToIndex);
}

// ----- local helpers (top-level local functions) -----

void PrintUsage()
{
    Console.WriteLine("LagoVista.AI.Rag Ingestor");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run                         # Default: index mode, repo 'backend'");
    Console.WriteLine("  dotnet run --mode=index            # Explicit index mode");
    Console.WriteLine("  dotnet run --mode=index --repo=MyRepo");
    Console.WriteLine("  dotnet run --mode=subkind          # SubKind test for all configured repos");
    Console.WriteLine("  dotnet run --mode=subkind --repo=MyRepo");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --mode=index       Run full indexing pipeline (current default behavior).");
    Console.WriteLine("  --mode=subkind     Run SubKindDetector test mode only; no vector DB writes.");
    Console.WriteLine("  --repo=<id>        Restrict to a single repository id from cfg.Ingestion.Repositories.");
    Console.WriteLine("  --help, -h, /?     Show this help.");
    Console.WriteLine();
    Console.WriteLine("Subkind test mode:");
    Console.WriteLine("  - Uses cfg.Ingestion.SourceRoot and cfg.Ingestion.Repositories");
    Console.WriteLine("  - Walks *.cs files (respecting Include/Exclude via FileWalker)");
    Console.WriteLine("  - Calls SubKindDetector.DetectForFile on each file");
    Console.WriteLine("  - Prints per-file SubKind and a summary per repo");
    Console.WriteLine();
}

async Task RunSubKindTestAsync(IngestionConfig cfg, string repoFilter)
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

        // Reuse FileWalker to stay consistent with the orchestrator's file discovery rules.
        var allFiles = FileWalker
            .EnumerateFiles(repoRoot, include, exclude)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Console.WriteLine();
        Console.WriteLine($"[SubKindTest] Repo '{repoId}': {allFiles.Count} C# files found.");

        var byKind = new Dictionary<CodeSubKind, int>();
        int fileIndex = 1;

        var results = new List<SubKindDetectionResult>();

        foreach (var fullPath in allFiles)
        {
            var text = await System.IO.File.ReadAllTextAsync(fullPath);
            var relPath = Path.GetRelativePath(repoRoot, fullPath).Replace('\\', '/');

            var result = SubKindDetector.DetectForFile(text, relPath);

            if (!byKind.TryGetValue(result.SubKind, out var count))
                count = 0;
            byKind[result.SubKind] = count + 1;
            results.Add(result);

            Console.WriteLine($"  {fileIndex,4}/{allFiles.Count,4}  [{result.SubKind}] {relPath}  ({result.PrimaryTypeName})");
            fileIndex++;
        }

        Console.WriteLine("----\r\n");

        foreach(var result in results.Where(r => r.SubKind == CodeSubKind.Other))
        {
            Console.WriteLine($"[WARNING] Other  SubKind detected in file '{result.PrimaryTypeName}'");
        }

        Console.WriteLine();
        Console.WriteLine($"[SubKindTest] Repo '{repoId}' summary:");
        foreach (var kvp in byKind.OrderBy(k => k.Key))
        {
            Console.WriteLine($"  {kvp.Key,-18}: {kvp.Value} file(s)");
        }

        Console.WriteLine();
    }
}
