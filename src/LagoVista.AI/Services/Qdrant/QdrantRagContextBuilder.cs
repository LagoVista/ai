using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.Core.Utils.Types.Nuviot.RagIndexing;
using LagoVista.Core.Interfaces;
using LagoVista.Core.AI.Models;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.Core;

namespace LagoVista.AI.Services.Qdrant
{
    /// <summary>
    /// Default implementation that queries Qdrant and builds an AGN-002 compliant
    /// [CONTEXT] block from matching chunks.
    /// </summary>
    public class QdrantRagContextBuilder : IRagContextBuilder
    {
        private readonly IEmbedder _embedder;
        private readonly IQdrantClient _qdrantClient;
        private readonly ILLMContentRepo _contentRepo;
        private readonly int _topK;
        private readonly IAdminLogger _adminLogger;
        private readonly IAgentStreamingContext _agentStreamingContext;

        /// <summary>
        /// Creates a new <see cref="QdrantRagContextBuilder"/>.
        /// </summary>
        /// <param name="embedder">Embedder with an EmbedAsync(string, int) method that returns a vector result.</param>
        /// <param name="qdrantClient">Qdrant client abstraction.</param>
        /// <param name="contentRepo">Repository used to resolve raw text content for blobs/paths.</param>
        public QdrantRagContextBuilder(IEmbedder embedder, IQdrantClient qdrantClient, IAgentStreamingContext agentStreamingContext, ILLMContentRepo contentRepo, IAdminLogger adminLogger)
        {
            _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
            _qdrantClient = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
            _contentRepo = contentRepo ?? throw new ArgumentNullException(nameof(contentRepo));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
            _topK = 8;
        }

        /// <inheritdoc />
        public async Task<InvokeResult<IAgentPipelineContext>> BuildContextSectionAsync(IAgentPipelineContext piplineContext, string query)
        {
            if (null == piplineContext) throw new ArgumentNullException(nameof(piplineContext));

            if(String.IsNullOrEmpty(query))
            {
                return InvokeResult<IAgentPipelineContext>.FromError("Query is required.");
            }

            // 1) Embed the instructions
            var embedResult = await _embedder.EmbedAsync(query);
            if (!embedResult.Successful)
            {
                return InvokeResult<IAgentPipelineContext>.FromError(embedResult.ErrorMessage);
            }

            var vector = embedResult.Result.Vector as float[];
            if (vector == null || vector.Length == 0)
            {
                return InvokeResult<IAgentPipelineContext>.FromError("Embedding vector was empty.");
            }

            // 3) Retrieve candidates from Qdrant
            var searchRequest = new QdrantSearchRequest
            {
                Vector = vector,
                Limit = Math.Clamp(_topK * 3, 12, 50),
                WithPayload = true,
                Filter = piplineContext.Envelope.RagScope.Conditions.Count > 0 ? piplineContext.Envelope.RagScope : null
            };

            var hits = await _qdrantClient.SearchAsync(piplineContext.AgentContext.VectorDatabaseCollectionName, searchRequest);

            _adminLogger.Trace($"{this.Tag()} Query Completed, found {hits.Count} results.");

            if (hits == null || hits.Count == 0)
            {
                // Return an empty but well-formed context block
                return InvokeResult<IAgentPipelineContext>.Create(piplineContext);
            }

            // 4) Select a diverse subset
            var selected = SelectDiverse(hits, _topK);
            if (selected.Count == 0)
            {
                return InvokeResult<IAgentPipelineContext>.Create(piplineContext);
            }

            var results = await BuildContextBlockAsync(piplineContext.AgentContext, selected);
            piplineContext.AddRagContent(results);

            await _agentStreamingContext.AddMilestoneAsync($"Found {selected.Count} chunks of information.");
          

            return InvokeResult<IAgentPipelineContext>.Create(piplineContext);
        }

        /// <summary>
        /// Diverse top-K selection using path + symbol key, leveraging RagVectorPayload.FromDictionary.
        /// </summary>
        private static List<QdrantScoredPoint> SelectDiverse(IReadOnlyList<QdrantScoredPoint> hits, int topK)
        {
            var byKey = new Dictionary<string, QdrantScoredPoint>(StringComparer.OrdinalIgnoreCase);
            if (hits == null)
            {
                return new List<QdrantScoredPoint>();
            }

            foreach (var h in hits.OrderByDescending(h => h.Score))
            {
                if (h.Payload == null)
                {
                    continue;
                }

                var payload = h.Payload;

                var path = !string.IsNullOrWhiteSpace(payload.Extra.Path)
                    ? payload.Extra.Path
                    : !string.IsNullOrWhiteSpace(payload.Extra.ModelContentUrl)
                        ? payload.Extra.ModelContentUrl
                        : string.Empty;

                var sym = payload.Extra.SymbolName ?? string.Empty;
                var key = path + "::" + sym;

                if (!byKey.ContainsKey(key))
                {
                    byKey[key] = h;
                }

                if (byKey.Count >= topK)
                {
                    break;
                }
            }

            return byKey.Values.ToList();
        }

        /// <summary>
        /// Builds the full AGN-002 [CONTEXT] block from the selected points.
        /// </summary>
        private async Task<List<RagContent>> BuildContextBlockAsync(AgentContext agentContext, IReadOnlyList<QdrantScoredPoint> selected)
        {

            var contentItems = new List<RagContent>();

            var chunkIndex = 1;

            _adminLogger.Trace($"{this.Tag()} Building context block with {selected.Count} chunks.");

            foreach (var hit in selected)
            {
                if (hit.Payload == null)
                {
                    _adminLogger.AddError(this.Tag(), "Query Completed, did not have have payload.");
                    continue;
                }

                var payload = hit.Payload;

                _adminLogger.Trace($"{this.Tag()} Processing payload {payload.Meta.SemanticId}, path {payload.Extra.SourceSliceBlobUri}.");

                var startLine = payload.Extra.LineStart ?? payload.Extra.StartLine ?? 1;
                var endLine = payload.Extra.LineEnd ?? payload.Extra.EndLine ?? startLine;
                if (endLine < startLine)
                {
                    endLine = startLine;
                }

                if (string.IsNullOrWhiteSpace(payload.Extra.ModelContentUrl))
                {
                    _adminLogger.AddCustomEvent(Core.PlatformSupport.LogLevel.Warning, this.Tag(), $"{payload.Meta.Title} does not have ModelContentUrl", payload.Meta.Title.ToKVP("title"));
                    continue;
                }
                var uri = new Uri(payload.Extra.ModelContentUrl);
                var startFileName = uri.AbsolutePath.Substring(1).IndexOf('/');    
                var fileName = uri.AbsolutePath.Substring(startFileName + 1);
                _adminLogger.Trace($"{this.Tag()} - Requesting blob {payload.Meta.OrgNamespace} - {fileName}");

                var contentResult = await _contentRepo.GetTextContentAsync(payload.Meta.OrgNamespace, fileName);
                if (!contentResult.Successful || string.IsNullOrWhiteSpace(contentResult.Result))
                {
                    _adminLogger.AddCustomEvent( Core.PlatformSupport.LogLevel.Warning, this.Tag(), $"Could Not Load ModelContentUrl - {payload.Extra.ModelContentUrl }", payload.Meta.Title.ToKVP("title"));
                    continue;
                }

                contentItems.Add(new RagContent()
                {
                    PointId = hit.Id,
                    FullContentUrl = payload.Extra.FullDocumentBlobUri,
                    HumanContentUrl = payload.Extra.HumanContentUrl,
                    ModelContent = contentResult.Result,
                    RagContentIndex = chunkIndex++,
                    ScemanticId = payload.Meta.SemanticId,
                    Title = payload.Meta.Title
                });

                _adminLogger.Trace($"{this.Tag()} Added chunk for {payload.Meta.Title} {payload.Meta.Subtype}");

            }

            await _agentStreamingContext.AddMilestoneAsync($"Added {chunkIndex} of those chunks.");

            return contentItems;
        }

        /// <summary>
        /// Slice a multi-line text blob by 1-based line numbers. If bounds are invalid,
        /// they are clamped to the actual text.
        /// </summary>
        private static string SliceLines(string text, int? start, int? end)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            var startLine = start ?? 1;
            var endLine = end ?? lines.Length;

            if (startLine < 1)
            {
                startLine = 1;
            }

            if (endLine < startLine)
            {
                endLine = startLine;
            }

            if (endLine > lines.Length)
            {
                endLine = lines.Length;
            }

            var sb = new StringBuilder();
            for (var i = startLine - 1; i <= endLine - 1; i++)
            {
                sb.AppendLine(lines[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Very small helper to map file extensions to AGN-002 language tags.
        /// </summary>
        private static string InferLanguageFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "plaintext";
            }

            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".cs": return "csharp";
                case ".ts": return "typescript";
                case ".tsx": return "tsx";
                case ".js": return "javascript";
                case ".json": return "json";
                case ".scss":
                case ".css": return "scss";
                case ".xml": return "xml";
                case ".md": return "markdown";
                default: return "plaintext";
            }
        }
    }
}
