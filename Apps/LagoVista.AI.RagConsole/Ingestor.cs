using LagoVista.AI.Interfaces;
using LagoVista.AI.Services;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Logging.Utils;
using RagCli.Services;
using RagCli.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LagoVista.AI.RagConsole
{
    public static class Ingestor
    {
        public static async Task Ingest()
        {

            var cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText("appsettings.json"))!;
            var qcfg = new QdrantConfig()
            {
                QdrantApiKey = Env.Get("QDRANT_API_KEY", cfg.Qdrant.ApiKey),
                QdrantEndpoint = Env.Get("QDRANT_ENDPOINT", cfg.Qdrant.Endpoint)
            };

            var qdrant = new QdrantClient(qcfg);
            var collectionName = cfg.Qdrant.Collection;

            // 1) Ensure collection exists
            await qdrant.EnsureCollectionAsync(new QdrantCollectionConfig
            {
                Name = collectionName,
                VectorSize = cfg.Qdrant.VectorSize,
                Distance = cfg.Qdrant.Distance
            });

            var settings = new OpenAiConfig()
            {
                OpenAIApiKey = Env.Get("OPENAI_API_KEY", cfg.Embeddings.ApiKey),
                OpenAIUrl = string.IsNullOrWhiteSpace(cfg.Embeddings.BaseUrl) ? "https://api.openai.com" : cfg.Embeddings.BaseUrl
            };

            var inline = new FileManifestTrackerInline();

            // 2) Ingest & index
            var chunker = new RoslynCSharpChunker(cfg.Ingestion.MaxTokensPerChunk, cfg.Ingestion.OverlapLines);
            IEmbedder embedder = new OpenAIEmbedder(settings, new AdminLogger(new ConsoleLogWriter()));

            foreach (var repo in cfg.Ingestion.Repositories)
            {
                var fullRoot = Path.Combine(cfg.Ingestion.SourceRoot, repo);

                var registry = new IndexRegistry(fullRoot);

                var files = FileWalker.EnumerateFiles(fullRoot, cfg.Ingestion.Include, cfg.Ingestion.Exclude);
                var removeIds = registry.RemoveMissing(files);

                var toIndex = inline.GetFilesNeedingIndex(files.Select(p => Path.Combine(fullRoot, p)));

                foreach (var file in toIndex)
                {
                    var relPath = Path.GetRelativePath(repo, file);
                    var text = await File.ReadAllTextAsync(file);
                    var chunks = chunker.Chunk(text, relPath);

                    var points = new List<QdrantPoint>();
                    foreach (var ch in chunks)
                    {
                        var vec = await embedder.EmbedAsync(ch.Text);
                        points.Add(new QdrantPoint
                        {
                            Id = QdrantPoint.NewId(),
                            Vector = vec,
                            Payload = new Dictionary<string, object?>
                            {
                                ["repo"] = Path.GetFileName(repo),
                                ["path"] = relPath,
                                ["language"] = LanguageGuesser.FromPath(relPath),
                                ["symbol"] = ch.Symbol,
                                ["start_line"] = ch.StartLine,
                                ["end_line"] = ch.EndLine,
                                ["kind"] = ch.Kind
                            }
                        });

                        Console.WriteLine($"\t  Chunk: {relPath} {ch.Kind} [{ch.StartLine}-{ch.EndLine}] symbol={ch.Symbol} vec_len={vec.Length}");
                    }

                    if (points.Count > 0)
                    {
                        registry.Upsert(relPath, points.Select(p => p.Id));
                        int retryCount = 0;
                        while (retryCount++ < 5)
                        {
                            try
                            {
                                Console.WriteLine($"\tUpserting {points.Count} chunks from {relPath}...");
                                await qdrant.UpsertAsync(collectionName, points);
                                inline.UpsertInlineHeader(file);
                                break;
                            }
                            catch (Exception ex)
                            {
                                Console.Write("Error - uploading to Qdrant: " + ex.Message + " retry count " + retryCount.ToString());
                                await Task.Delay(retryCount * 2000);
                            }
                        }
                    }
                }

                registry.Save();
            }

            Console.WriteLine("Ingestion complete.\n");

            // 3) Query loop
            while (true)
            {
                Console.Write("Query> ");
                var q = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(q)) break;

                var qVec = await embedder.EmbedAsync(q);

                var results = await qdrant.SearchAsync(collectionName, new QdrantSearchRequest
                {
                    Vector = qVec,
                    Limit = 8,
                    WithPayload = true,
                    Filter = new QdrantFilter
                    {
                        Must = new()
            {
                new QdrantCondition { Key = "language", Match = new() { Value = "csharp" } }
            }
                    }
                });

                foreach (var r in results)
                {
                    var pl = r.Payload!;
                    Console.WriteLine($"score={r.Score:F3} {pl["repo"]}/{pl["path"]} [{pl["start_line"]}-{pl["end_line"]}] symbol={pl.GetValueOrDefault("symbol", "-")}");
                }
            }

        }
    }

    class OpenAiConfig : IOpenAISettings
    {
        public string OpenAIUrl { get; set; }

        public string OpenAIApiKey { get; set; }
    }

    class QdrantConfig : IQdrantSettings
    {
        public string QdrantEndpoint { get; set; }

        public string QdrantApiKey { get; set; }
    }
}
