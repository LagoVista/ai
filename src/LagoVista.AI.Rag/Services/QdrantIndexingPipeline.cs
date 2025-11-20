using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.CloudRepos;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Services;
using LagoVista.Core.Models;
using LagoVista.Core.Utils;
using LagoVista.Core.Utils.Types;
using LagoVista.IoT.Logging;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Default indexing pipeline implementation that wires chunking, embeddings,
    /// Qdrant and content repository together.
    ///
    /// This is intentionally focused on library concerns only; orchestration,
    /// configuration loading, and console UX live elsewhere.
    ///
    /// Current behavior:
    /// - C# files: use <see cref="RoslynCSharpChunker"/>.
    /// - Other text files: single raw chunk.
    /// - Per file:
    ///   - Ensure Qdrant collection exists.
    ///   - Chunk and embed.
    ///   - Build payloads via <see cref="RagPayloadFactory"/>.
    ///   - Delete existing chunks for DocId (when available) or Repo+Path.
    ///   - Upsert new chunks.
    ///   - Store full text in <see cref="LLMContentRepo"/>.
    /// </summary>
    public class QdrantIndexingPipeline : IIndexingPipeline
    {
        private readonly IAdminLogger _logger;

        private bool _initialized;
        private bool _collectionEnsured;
        private string _collectionName;

        private RoslynCSharpChunker _csharpChunker;
        private OpenAIEmbedder _embedder;
        private QdrantClient _qdrant;
        private LLMContentRepo _contentRepo;

        public QdrantIndexingPipeline(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task IndexFileAsync(IndexFileContext context, CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            EnsureInitialized(context.Config, context.AgentContext);
            await EnsureCollectionAsync(context.Config);

            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(context.FullPath);
            var ext = fileInfo.Extension;

            var text = await System.IO.File.ReadAllTextAsync(context.FullPath, cancellationToken);

            // Canonical path and DocId per IDX-001 / IDX-003
            var canonicalPath = IndexIds.BuildCanonicalPath(context.RepoId, context.RepoRelativePath);
            var docId = IndexIds.ComputeDocId(context.RepoInfo.RemoteUrl, canonicalPath);

            // Persist DocId into local index record so missing-file deletion can use it later.
            if (context.LocalRecord != null)
            {
                context.LocalRecord.DocId = docId;
            }

            // Build chunk plan
            RagChunkPlan plan;
            if (string.Equals(ext, ".cs", StringComparison.OrdinalIgnoreCase))
            {
                plan = _csharpChunker.Chunk(text, context.RepoRelativePath, canonicalPath);
            }
            else
            {
                // Simple fallback: single text chunk for non-C# files for now.
                var lines = text.Split('\n');

                var chunk = new RagChunk
                {
                    EstimatedTokens = TokenEstimator.EstimateTokens(text),
                    TextNormalized = text,
                    LineStart = 1,
                    LineEnd = lines.Length,
                    Symbol = fileInfo.Name,
                    SymbolType = "file",
                    SectionKey = "file"
                };

                plan = new RagChunkPlan
                {
                    Chunks = new List<RagChunk> { chunk },
                    Raw = new RawArtifact
                    {
                        IsText = true,
                        MimeType = "text/plain",
                        SuggestedBlobPath = canonicalPath,
                        Text = text
                    }
                };
            }

            if (plan.Chunks == null || plan.Chunks.Count == 0)
            {
                _logger.Trace($"[QdrantIndexingPipeline] No chunks produced for '{context.RepoRelativePath}'. Skipping.");
                return;
            }

            // Embed each chunk
            var idx = 0;
            foreach (var chunk in plan.Chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                idx++;
                chunk.Vector = await _embedder.EmbedAsync(chunk.TextNormalized, chunk.EstimatedTokens);
            }

            // Build metadata contexts for payloads
            var ingestContext = new IngestContext
            {
                EmbeddingModel = context.Config.Embeddings.Model,
                IndexVersion = context.Config.IndexVersion,
                OrgId = context.Config.OrgId,
                ProjectId = context.RepoId
            };

            var artifactContext = new CodeArtifactContext
            {
                Language = string.Equals(ext, ".cs", StringComparison.OrdinalIgnoreCase) ? "C#" : "text",
                CommitSha = context.RepoInfo.CommitSha,
                Path = canonicalPath,
                Repo = context.RepoInfo.RemoteUrl,
                RepoBranch = context.RepoInfo.BranchRef,
                Subtype = "server"
            };

            var payloads = RagPayloadFactory.FromCodePlan(plan, ingestContext, artifactContext);

            // Delete existing chunks for this DocId (preferred) or fall back to Repo+Path
            var filterConditions = new List<QdrantCondition>();

            if (!string.IsNullOrWhiteSpace(docId))
            {
                filterConditions.Add(new QdrantCondition
                {
                    Key = "DocId",
                    Match = new QdrantMatch
                    {
                        Value = docId
                    }
                });
            }
            else
            {
                filterConditions.Add(new QdrantCondition
                {
                    Key = "Repo",
                    Match = new QdrantMatch
                    {
                        Value = context.RepoInfo.RemoteUrl
                    }
                });

                filterConditions.Add(new QdrantCondition
                {
                    Key = "Path",
                    Match = new QdrantMatch
                    {
                        Value = canonicalPath
                    }
                });
            }

            var deleteFilter = new QdrantFilter
            {
                Must = filterConditions
            };

            await _qdrant.DeleteByFilterAsync(_collectionName, deleteFilter);

            if (payloads.Count > 0)
            {
                await _qdrant.UpsertInBatchesAsync(_collectionName, payloads, vectorDims: context.Config.Qdrant.VectorSize);

                // Persist raw file text to content repo for retrieval.
                var folder = BuildContentFolder(context.RepoId, context.RepoRelativePath);
                await _contentRepo.AddTextContentAsync(context.AgentContext, folder, fileInfo.Name, text, "text/plain");
            }
        }

        public async Task HandleMissingFileAsync(MissingFileContext context, CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            EnsureInitialized(context.Config, context.AgentContext);
            await EnsureCollectionAsync(context.Config);

            cancellationToken.ThrowIfCancellationRequested();

            string docId = context.Record?.DocId;

            if (string.IsNullOrWhiteSpace(docId))
            {
                // Compute DocId from Repo + canonical path if not stored yet.
                var canonicalPath = IndexIds.BuildCanonicalPath(context.RepoId, context.Record.FilePath);
                docId = IndexIds.ComputeDocId(context.RepoInfo.RemoteUrl, canonicalPath);
            }

            var conditions = new List<QdrantCondition>();

            if (!string.IsNullOrWhiteSpace(docId))
            {
                conditions.Add(new QdrantCondition
                {
                    Key = "DocId",
                    Match = new QdrantMatch
                    {
                        Value = docId
                    }
                });
            }
            else
            {
                // Absolute fallback: delete by Repo+Path if DocId is unavailable.
                var canonicalPath = IndexIds.BuildCanonicalPath(context.RepoId, context.Record.FilePath);

                conditions.Add(new QdrantCondition
                {
                    Key = "Repo",
                    Match = new QdrantMatch
                    {
                        Value = context.RepoInfo.RemoteUrl
                    }
                });

                conditions.Add(new QdrantCondition
                {
                    Key = "Path",
                    Match = new QdrantMatch
                    {
                        Value = canonicalPath
                    }
                });
            }

            if (conditions.Count == 0)
            {
                _logger.Trace("[QdrantIndexingPipeline] Missing-file deletion skipped because no filter conditions could be constructed.");
                return;
            }

            var filter = new QdrantFilter
            {
                Must = conditions
            };

            await _qdrant.DeleteByFilterAsync(_collectionName, filter);
        }

        private void EnsureInitialized(IngestionConfig config, AgentContext agentContext)
        {
            if (_initialized) return;

            if (config == null) throw new ArgumentNullException(nameof(config));
            if (agentContext == null) throw new ArgumentNullException(nameof(agentContext));

            var qdrantSettings = new QdrantSettingsAdapter
            {
                QdrantEndpoint = config.Qdrant.Endpoint,
                QdrantApiKey = config.Qdrant.ApiKey
            };

            _qdrant = new QdrantClient(qdrantSettings, _logger);

            var openAiSettings = new OpenAISettingsAdapter
            {
                OpenAIUrl = string.IsNullOrWhiteSpace(config.Embeddings.BaseUrl) ? "https://api.openai.com" : config.Embeddings.BaseUrl,
                OpenAIApiKey = config.Embeddings.ApiKey
            };

            _embedder = new OpenAIEmbedder(openAiSettings, _logger);

            var aiSettings = new AiSettings
            {
                MLBlobStorage = new ConnectionSettings
                {
                    AccountId = config.ContentRepo.AccountId,
                    AccessKey = config.ContentRepo.AccessKey
                }
            };

            _contentRepo = new LLMContentRepo(aiSettings, _logger);

            _csharpChunker = new RoslynCSharpChunker(config.Ingestion.MaxTokensPerChunk, config.Ingestion.OverlapLines);

            _collectionName = string.IsNullOrWhiteSpace(config.Qdrant.Collection) ? agentContext.VectorDatabaseCollectionName : config.Qdrant.Collection;

            _initialized = true;
        }

        private async Task EnsureCollectionAsync(IngestionConfig config)
        {
            if (_collectionEnsured) return;

            var collectionConfig = new QdrantCollectionConfig
            {
                VectorSize = config.Qdrant.VectorSize,
                Distance = config.Qdrant.Distance
            };

            await _qdrant.EnsureCollectionAsync(collectionConfig, _collectionName);
            _collectionEnsured = true;
        }

        private static string BuildContentFolder(string repoId, string repoRelativePath)
        {
            if (string.IsNullOrWhiteSpace(repoId)) repoId = "unknown-repo";

            var directory = Path.GetDirectoryName(repoRelativePath) ?? string.Empty;
            directory = directory.Replace('\\', '/').Trim('/');

            if (string.IsNullOrEmpty(directory)) return repoId;

            return repoId + "/" + directory;
        }

        private class QdrantSettingsAdapter : IQdrantSettings
        {
            public string QdrantEndpoint { get; set; }
            public string QdrantApiKey { get; set; }
        }

        private class OpenAISettingsAdapter : IOpenAISettings
        {
            public string OpenAIUrl { get; set; }
            public string OpenAIApiKey { get; set; }
        }
    }
}
