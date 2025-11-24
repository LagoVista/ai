// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 84feeaeb64610697ce02a858eb217cf406b5821707963650d179e5c3e6374064
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.Models;
using LagoVista.Core.Models;
using Newtonsoft.Json;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Services;
using LagoVista.AI.Rag.ContractPacks.Infrastructure.Services;
using LagoVista.Core.IOC;
using LagoVista.AI.Rag.ContractPacks.Orchestration.Interfaces;
using LagoVista.AI.Rag;

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

            LagoVista.AI.Rag.Chunkers.Startup.Init();
            LagoVista.AI.Rag.Startup.Init();
          
            if (showHelp)
            {
                PrintUsage();
                return;
            }

            var configLoader = new JsonIngestionConfigProvider();
            var json = System.IO.File.ReadAllText("appsettings.json");
            var result = await configLoader.LoadAsync(json);
            if (!result.Successful)
            {
                Console.WriteLine(result.ErrorMessage);
                return;
            }

            Console.WriteLine($"Repo: {repoId}");

            var orchestrator = SLWIOC.Create<IIndexRunOrchestrator>();
            await orchestrator.RunAsync(result.Result);


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
    }
}
