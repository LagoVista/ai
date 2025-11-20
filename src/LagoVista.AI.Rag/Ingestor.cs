// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 1621ff7de0f4af2130ba69788a09d979f4aa17785bfa044f3ac9c44abcdeba00
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.CloudRepos;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Rag.Services;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Services;
using LagoVista.Core.Models;
using LagoVista.Core.Utils;
using LagoVista.IoT.Logging;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Logging.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
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

            collectionName += "2";

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
            var embedder = new OpenAIEmbedder(settings, new AdminLogger(new ConsoleLogWriter()));

            var repositoryIndex = 1;
            foreach (var repo in _config.Ingestion.Repositories)
            {
                var fullRoot = Path.Combine(_config.Ingestion.SourceRoot, repo);

                if (!System.IO.Directory.Exists(fullRoot))
                    throw new Exception("Could not open directory: " + fullRoot);
                
                if (!GitRepoInspector.TryGetRepoInfo(fullRoot, out RepoInfo info, out string error))
                    throw new Exception($"Could not get repo information: {error}");

                var registry = new IndexRegistry(fullRoot);

                var files = FileWalker.EnumerateFiles(fullRoot, _config.Ingestion.Include, _config.Ingestion.Exclude);
                var removeIds = registry.RemoveMissing(files);

                var toIndex = inline.GetFilesNeedingIndex(files.Select(p => Path.Combine(fullRoot, p)), _config.IndexVersion);
                //var toIndex = files;
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


                    var indexRepo = file.IndexOf(repo);
                    if (indexRepo == -1)
                        Debugger.Break();

                    var pathInProject = file.Substring(indexRepo);
                    var blobUri = $"/{pathInProject.Replace('\\','/')}".ToLower();

                    pathInProject = pathInProject.Replace(fileInfo.Name, String.Empty);
                    pathInProject = pathInProject.TrimEnd(Path.DirectorySeparatorChar);

                    var pathInRepo = pathInProject.Replace(repo, "").Replace('\\', '/') + $"/{fileInfo.Name}";

                    var plan = chunker.Chunk(text, relPath, blobUri);
                    adminLogger.Trace($"[Ingestor__IngestAsync] {fileInfo.Name} - found {plan.Chunks.Count} in {sw.Elapsed.TotalMilliseconds}ms");

                    var idx = 0;

                    foreach (var chunk in plan.Chunks)
                    {
                        sw.Restart();
                        chunk.Vector = await embedder.EmbedAsync(chunk.TextNormalized, chunk.EstimatedTokens);
                        adminLogger.Trace($"[Ingestor__IngestAsync] {fileInfo.Name} {chunk.Symbol}/{chunk.SymbolType} - chunk {idx++} of {plan.Chunks.Count} in {sw.Elapsed.TotalMilliseconds}ms, {Math.Round((idx * 100.0f/plan.Chunks.Count))}% ");
                    }

                    var payloads = RagPayloadFactory.FromCodePlan(plan, new Core.Utils.Types.IngestContext()
                    {
                        EmbeddingModel = _config.Embeddings.Model,
                        IndexVersion = _config.IndexVersion,
                        OrgId = _config.OrgId,
                        ProjectId = repo
                    }, new Core.Utils.Types.CodeArtifactContext()
                    {
                         Language = "C#",
                         CommitSha = info.CommitSha,
                         Path = pathInRepo,
                         Repo = info.RemoteUrl,
                         RepoBranch = info.BranchRef,
                         Subtype = "server"
                    });

                    sw.Restart();
                    await qdrant.DeleteByFilterAsync(collectionName, new QdrantFilter()
                    {
                        Must = new List<QdrantCondition>()
                        {
                            new QdrantCondition()
                            {
                                Key = "Repo",
                                Match = new QdrantMatch()
                                {
                                    Value = info.RemoteUrl
                                }
                            },
                            new QdrantCondition()
                            {
                                Key = "Path",
                                Match = new QdrantMatch()
                                {
                                    Value = pathInRepo
                                }
                            }
                        }
                    });

                    adminLogger.Trace($"[Ingestor__IngestAsync] Removed Old Vectors for {info.RemoteUrl} {pathInRepo} in {sw.Elapsed.TotalMilliseconds}ms.");

                    var result = await contentRepo.AddTextContentAsync(_agentContext, pathInProject, fileInfo.Name, text, "text/plain");
                   
                    if (result.Successful && payloads.Count > 0)
                    {
                        registry.Upsert(relPath, payloads.Select(p => p.PointId));
                        registry.Save();
                        
                        int retryCount = 0;
                        while (retryCount++ < 5)
                        {
                            try
                            {
                                await qdrant.UpsertInBatchesAsync(collectionName, payloads, vectorDims:3072);
                                adminLogger.Trace($"[Ingestor__IngestAsync] {fileInfo.Name} uploaded {payloads.Count} points to qdrant in {sw.Elapsed.TotalMilliseconds}ms");

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
