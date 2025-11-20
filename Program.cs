// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 84feeaeb64610697ce02a858eb217cf406b5821707963650d179e5c3e6374064
// IndexVersion: 2
// --- END CODE INDEX META ---
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.AI.Rag;
using LagoVista.AI.Rag.Services;
using LagoVista.AI.Rag.Types;
using LagoVista.Core.Models;
using Newtonsoft.Json;

// Tiny console front-end:
// - parse args
// - load config + AgentContext
// - either run normal indexing (Ingestor) or SubKind test (SubKindTestRunner)

var (mode, repoId, showHelp) = ParseArgs(args);

if (showHelp)
{
    PrintUsage();
    return;
}

// Load config (same as original code)
var jsonConfig = File.ReadAllText("appsettings.json");
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

var vectoDb = new AgentContext
{
    VectorDatabaseCollectionName = cfg.Qdrant.Collection,
    VectorDatabaseApiKey = cfg.Qdrant.ApiKey,
    VectorDatabaseUri = cfg.Qdrant.Endpoint,
    LlmApiKey = cfg.Embeddings.ApiKey,
    AzureAccountId = cfg.ContentRepo.AccountId,
    AzureApiToken = cfg.ContentRepo.AccessKey,
    OwnerOrganization = new EntityHeader
    {
        Id = cfg.OrgId,
    }
};

if (string.Equals(mode, "subkind", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("Mode: subkind test");
    Console.WriteLine($"Repo filter: {(string.IsNullOrWhiteSpace(repoId) ? "<all configured repos>" : repoId)}");
    Console.WriteLine();

    var results = await SubKindTestRunner.RunAsync(cfg, repoId, CancellationToken.None);

    if (results.Count == 0)
    {
        Console.WriteLine("No C# files found for SubKind test.");
        return;
    }

    // Simple summary by SubKind
    var grouped = results
        .GroupBy(r => r.SubKindString)
        .OrderBy(g => g.Key);

    Console.WriteLine("SubKind summary:");
    foreach (var g in grouped)
    {
        Console.WriteLine($"  {g.Key,-18}: {g.Count()} file(s)");
    }

    Console.WriteLine();
    Console.WriteLine("Sample files (up to 10):");
    foreach (var r in results.Take(10))
    {
        Console.WriteLine($"  [{r.SubKindString}] {r.RepoId}/{r.RepoRelativePath} ({r.PrimaryTypeName})");
    }

    Console.WriteLine();
}
else
{
    // Preserve existing behavior for indexing
    var repoToIndex = string.IsNullOrWhiteSpace(repoId) ? "backend" : repoId;

    Console.WriteLine("Mode: index");
    Console.WriteLine($"Repo: {repoToIndex}");
    Console.WriteLine();

    var ingestor = new Ingestor(cfg, vectoDb);
    await ingestor.IngestAsync(repoToIndex);
}

// ---------------- local helpers ----------------

static (string mode, string repoId, bool showHelp) ParseArgs(string[] args)
{
    var mode = "index"; // default
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
        else if (arg.StartsWith("--repo=", StringComparison.OrdinalIgnoreCase))
        {
            repoId = arg.Substring("--repo=".Length);
        }
        else
        {
            Console.WriteLine($"Unknown argument: {arg}");
            showHelp = true;
        }
    }

    return (mode, repoId, showHelp);
}

static void PrintUsage()
{
    Console.WriteLine("LagoVista.AI.Rag Ingestor");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run                         # Default: index mode, repo 'backend'");
    Console.WriteLine("  dotnet run --mode=index            # Explicit index mode");
    Console.WriteLine("  dotnet run --mode=index --repo=MyRepo");
    Console.WriteLine("  dotnet run --mode=subkind          # SubKind test over all configured repos");
    Console.WriteLine("  dotnet run --mode=subkind --repo=MyRepo");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --mode=index       Run full indexing pipeline (current default behavior).");
    Console.WriteLine("  --mode=subkind     Run SubKindDetector test mode only; no vector DB writes.");
    Console.WriteLine("  --repo=<id>        Restrict to a single repository id from cfg.Ingestion.Repositories.");
    Console.WriteLine("  --help, -h, /?     Show this help.");
    Console.WriteLine();
}
