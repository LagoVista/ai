// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 84feeaeb64610697ce02a858eb217cf406b5821707963650d179e5c3e6374064
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.ContractPacks.Infrastructure.Services;
using LagoVista.Core.IOC;
using LagoVista.AI.Rag.ContractPacks.Orchestration.Interfaces;
using LagoVista.AI.Services;
using LagoVista.AI.Interfaces;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Logging.Utils;
using LagoVista.Core.Interfaces;
using LagoVista.AI.Services.OpenAI;

namespace LagoVista.AI.RagConsole
{
    public static class Program
    {
        // Parse command-line arguments
        // Modes: "index" | "subkind" | "resources"
        static string mode = "index";
        static string? repoId = "";
        static bool showHelp = false;
        static bool verbose = false;
        static bool dryRun = false;
        static SubtypeKind? subKindFilter;

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
                    else if (value.Equals("refine", StringComparison.OrdinalIgnoreCase))
                        mode = "refine";
                    else if (value.Equals("domaincatalog", StringComparison.OrdinalIgnoreCase))
                        mode = "domaincatalog";
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
                else if (arg.StartsWith("--subkindfilter=", StringComparison.OrdinalIgnoreCase))
                {
                    var contentType = arg.Substring("--subkindfilter=".Length);
                    if (Enum.TryParse<SubtypeKind>(contentType, true, out var parsed))
                    {
                        subKindFilter = parsed;
                    }
                    else
                    {
                        Console.WriteLine($"Unknown subkind filter '{contentType}'.");
                        showHelp = true;
                    }
                }
                else if (arg.Equals("--repo", StringComparison.OrdinalIgnoreCase))
                {
                    showHelp = true;
                    Console.WriteLine("RepoId");
                }
                else if(arg.Equals("--verbose"))
                {
                    verbose = true;
                    Console.WriteLine("Verbose Logging");
                }
                else if (arg.Equals("--vdryrun"))
                {
                    dryRun = true;
                    Console.WriteLine("Dry Run Mode");
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

            var configLoader = new JsonIngestionConfigProvider();
            var json = System.IO.File.ReadAllText("appsettings.json");
            var result = await configLoader.LoadAsync(json);
            if (!result.Successful)
            {
                Console.WriteLine(result.ErrorMessage);
                return;
            }

            var adminLogger = new AdminLogger(new ConsoleLogWriter());
            SLWIOC.RegisterSingleton<IAdminLogger>(adminLogger);
            var openAISettings = new OpenAISettings(result.Result.Embeddings.BaseUrl, result.Result.Embeddings.ApiKey);
            var qdrantSettings = new QdrantSettings(result.Result.Qdrant.Endpoint, result.Result.Qdrant.ApiKey);

            var embedder = new OpenAIEmbedder(openAISettings, adminLogger);

            SLWIOC.RegisterSingleton<IngestionConfig>(result.Result);
            SLWIOC.RegisterSingleton<IOpenAISettings>(openAISettings);
            SLWIOC.RegisterSingleton<IQdrantSettings>(qdrantSettings);

            var collection = new ServicesCollectionAdapter();

            LagoVista.AI.Startup.ConfigureServices(collection, adminLogger);

            var orchestrator = SLWIOC.Create<IIndexRunOrchestrator>();
            await orchestrator.RunAsync(result.Result, mode, repoId, subKindFilter, verbose, dryRun);
        }

        private class ServicesCollectionAdapter : IServiceCollection
        {
            public void AddScoped(Type serviceType)
            {
            }

            public void AddScoped(Type serviceType, Func<IServiceProvider, object> implementationFactory)
            {
                throw new NotImplementedException();
            }

            public void AddScoped(Type serviceType, Type implementationType)
            {
                throw new NotImplementedException();
            }

            public void AddScoped<TService>() where TService : class
            {
                throw new NotImplementedException();
            }

            public void AddScoped<TService>(Func<IServiceProvider, TService> implementationFactory) where TService : class
            {
                throw new NotImplementedException();
            }

            public void AddSingleton(Type serviceType)
            {
                throw new NotImplementedException();
            }

            public void AddSingleton(Type serviceType, Func<IServiceProvider, object> implementationFactory)
            {
                throw new NotImplementedException();
            }

            public void AddSingleton(Type serviceType, object implementationInstance)
            {
                throw new NotImplementedException();
            }

            public void AddSingleton(Type serviceType, Type implementationType)
            {
                throw new NotImplementedException();
            }

            public void AddSingleton<TService>() where TService : class
            {
                throw new NotImplementedException();
            }

            public void AddSingleton<TService>(Func<IServiceProvider, TService> implementationFactory) where TService : class
            {
                throw new NotImplementedException();
            }

            public void AddSingleton<TService>(TService implementationInstance) where TService : class
            {
                throw new NotImplementedException();
            }

            public void AddTransient(Type serviceType)
            {
                throw new NotImplementedException();
            }

            public void AddTransient(Type serviceType, Func<IServiceProvider, object> implementationFactory)
            {
                throw new NotImplementedException();
            }

            public void AddTransient(Type serviceType, Type implementationType)
            {
                throw new NotImplementedException();
            }

            public void AddTransient<TService>() where TService : class
            {
                throw new NotImplementedException();
            }

            public void AddTransient<TService>(Func<IServiceProvider, TService> implementationFactory) where TService : class
            {
                throw new NotImplementedException();
            }

            void IServiceCollection.AddScoped<TService, TImplementation>()
            {
                throw new NotImplementedException();
            }

            void IServiceCollection.AddScoped<TService, TImplementation>(Func<IServiceProvider, TImplementation> implementationFactory)
            {
                throw new NotImplementedException();
            }

            void IServiceCollection.AddSingleton<TService, TImplementation>()
            {
                throw new NotImplementedException();
            }

            void IServiceCollection.AddSingleton<TService, TImplementation>(Func<IServiceProvider, TImplementation> implementationFactory)
            {
                throw new NotImplementedException();
            }

            void IServiceCollection.AddTransient<TService, TImplementation>(Func<IServiceProvider, TImplementation> implementationFactory)
            {
                throw new NotImplementedException();
            }

            void IServiceCollection.AddTransient<TService, TImplementation>()
            {
                throw new NotImplementedException();
            }
        }

        internal class OpenAISettings : IOpenAISettings
        {
            public OpenAISettings(string url, string apiKey)
            {
                OpenAIUrl = url;
                OpenAIApiKey = apiKey;
            }

            public string OpenAIUrl { get; }

            public string OpenAIApiKey { get; }
        }

        internal class QdrantSettings : IQdrantSettings
        {
            public QdrantSettings(string endpoint, string apiKey)
            {
                QdrantEndpoint = endpoint;
                QdrantApiKey = apiKey;
            }

            public string QdrantEndpoint { get; }

            public string QdrantApiKey { get; }

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
