using LagoVista.AI.CloudRepos;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Rag.Services;
using LagoVista.AI.Rag.Types;
using LagoVista.AI.Services;
using LagoVista.Core.Models;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Logging.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag
{
    public class Ingestor
    {
        private readonly AgentContext _agentContext;
        private readonly IngestionConfig _config;

        public Ingestor(IngestionConfig config, AgentContext agentContext)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _agentContext = agentContext;
        }

        public async Task IngestAsync(string subModmain)
        {
            var adminLogger = new AdminLogger(new ConsoleLogWriter());

            var collectionName = _agentContext.VectorDatabaseCollectionName;

            var qcfg = new QdrantConfig()
            {
                QdrantApiKey = _agentContext.VectorDatabaseApiKey,
                QdrantEndpoint = _agentContext.VectorDatabaseUri
            };

            var qdrant = new QdrantClient(qcfg, adminLogger);

            // 1) Ensure collection exists
            await qdrant.EnsureCollectionAsync(new QdrantCollectionConfig
            {
                VectorSize = _config.Qdrant.VectorSize,
                Distance = _config.Qdrant.Distance
            }, collectionName);

            var settings = new OpenAiConfig()
            {
                OpenAIApiKey = _agentContext.LlmApiKey,
                OpenAIUrl = _config.Embeddings.BaseUrl
            };

            var inline = new FileManifestTrackerInline();
            var contentRepo = new LLMContentRepo(new AiSettings()
            {
                MLBlobStorage = new ConnectionSettings()
                {
                    AccountId = _agentContext.AzureAccountId,
                    AccessKey = _agentContext.AzureApiToken
                }
            }, adminLogger);

            // 2) Ingest & index
            var chunker = new RoslynCSharpChunker(_config.Ingestion.MaxTokensPerChunk, _config.Ingestion.OverlapLines);
            IEmbedder embedder = new OpenAIEmbedder(settings, new AdminLogger(new ConsoleLogWriter()));

            var repositoryIndex = 1;
            foreach (var repo in _config.Ingestion.Repositories)
            {

                var fullRoot = Path.Combine(_config.Ingestion.SourceRoot, repo);

                var registry = new IndexRegistry(fullRoot);

                var files = FileWalker.EnumerateFiles(fullRoot, _config.Ingestion.Include, _config.Ingestion.Exclude);
                var removeIds = registry.RemoveMissing(files);

                var toIndex = inline.GetFilesNeedingIndex(files.Select(p => Path.Combine(fullRoot, p)), _config.IndexVersion);

                var fileIndex = 1;

                var totalFileCount = toIndex.Count();

                foreach (var file in toIndex)
                {
                    var fullSw = Stopwatch.StartNew();
                    var sw = Stopwatch.StartNew();
                    var relPath = Path.GetRelativePath(repo, file);
                    var text = await System.IO.File.ReadAllTextAsync(file);
                    var fileInfo = new FileInfo(file);
                    adminLogger.Trace($"[Ingestor__IngestAsync] {fileInfo.Name} - starting repository {repositoryIndex} of {_config.Ingestion.Repositories.Count} repositories, file {fileIndex} of {totalFileCount} files");

                    var pathInProject = relPath.Substring(relPath.IndexOf(repo));
                    pathInProject = pathInProject.Replace(fileInfo.Name, String.Empty);
                    pathInProject = pathInProject.TrimEnd(Path.DirectorySeparatorChar);

                    var chunks = chunker.Chunk(text, relPath);
                    adminLogger.Trace($"[Ingestor__IngestAsync] {fileInfo.Name} - found {chunks} in {sw.Elapsed.TotalMilliseconds}ms");

                    var points = new List<QdrantPoint>();
                    foreach (var ch in chunks)
                    {
                        sw.Restart();
                        var vec = await embedder.EmbedAsync(ch.Text);
                        adminLogger.Trace($"[Ingestor__IngestAsync] {fileInfo.Name} - embedded {ch.Kind} [{ch.StartLine}-{ch.EndLine}] symbol={ch.Symbol} vec_len={vec.Length} in {sw.Elapsed.TotalMilliseconds}ms");
                        points.Add(new QdrantPoint
                        {
                            Id = QdrantPoint.NewId(),
                            Vector = vec,
                            Payload = new Dictionary<string, object>
                            {
                                ["repo"] = Path.GetFileName(repo),
                                ["fileName"] = fileInfo.Name,
                                ["path"] = pathInProject,
                                ["domain"] = "sourcecode",
                                ["subdomain"] = subModmain,
                                ["language"] = LanguageGuesser.FromPath(relPath),
                                ["symbol"] = ch.Symbol,
                                ["start_line"] = ch.StartLine,
                                ["end_line"] = ch.EndLine,
                                ["kind"] = ch.Kind
                            }
                        });
                    }
                     
                    var result = await contentRepo.AddTextContentAsync(_agentContext, pathInProject, fileInfo.Name, text, "text/plain");
                   
                    if (result.Successful && points.Count > 0)
                    {
                        registry.Upsert(relPath, points.Select(p => p.Id));
                        registry.Save();
                        
                        int retryCount = 0;
                        while (retryCount++ < 5)
                        {
                            try
                            {
                                await qdrant.UpsertInBatchesAsync(collectionName, points, vectorDims:3072);
                                adminLogger.Trace($"[Ingestor__IngestAsync] {fileInfo.Name} uploaded {points.Count} points to qdrant in {sw.Elapsed.TotalMilliseconds}ms");

                                inline.UpsertInlineHeader(file, _config.IndexVersion);
                                break;
                            }
                            catch (Exception ex)
                            {
                                Console.Write("Error - uploading to Qdrant: " + ex.Message + " retry count " + retryCount.ToString());
                                await Task.Delay(retryCount * 2000);
                            }
                        }
                    }

                    var percentComplete = (fileIndex * 100.0) / totalFileCount;

                    adminLogger.Trace($"[Ingestor__IngestAsync] {fileInfo.Name} - completed in {fullSw.Elapsed.TotalMilliseconds}ms repository {repositoryIndex} of {_config.Ingestion.Repositories.Count} repositories, file {fileIndex} of {totalFileCount} files {percentComplete.ToString("0.00")}%");
                    Console.WriteLine("---");
                    Console.WriteLine();
                    fileIndex++;

                }

                repositoryIndex++;
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
                        Must = new List<QdrantCondition>()
                        {
                            new QdrantCondition { Key = "language", Match = new QdrantMatch() { Value = "csharp" } }
                        }
                    }
                });

                foreach (var r in results)
                {
                    var pl = r.Payload!;
                    Console.WriteLine($"score={r.Score:F3} {pl["repo"]}\\{pl["path"]} [{pl["start_line"]}-{pl["end_line"]}] symbol={pl.GetValueOrDefault("symbol", "-")}");
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
