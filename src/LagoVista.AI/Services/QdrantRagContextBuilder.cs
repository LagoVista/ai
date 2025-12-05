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

namespace LagoVista.AI.Services
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

        /// <summary>
        /// Creates a new <see cref="QdrantRagContextBuilder"/>.
        /// </summary>
        /// <param name="embedder">Embedder with an EmbedAsync(string, int) method that returns a vector result.</param>
        /// <param name="qdrantClient">Qdrant client abstraction.</param>
        /// <param name="contentRepo">Repository used to resolve raw text content for blobs/paths.</param>
        public QdrantRagContextBuilder(
            IEmbedder embedder,
            IQdrantClient qdrantClient,
            ILLMContentRepo contentRepo,
            IAdminLogger adminLogger)
        {
            _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
            _qdrantClient = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
            _contentRepo = contentRepo ?? throw new ArgumentNullException(nameof(contentRepo));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _topK = 8;
        }

        /// <inheritdoc />
        public async Task<InvokeResult<string>> BuildContextSectionAsync(AgentContext agentContext,
            string instructions,
            RagScopeFilter filter)
        {
            if (string.IsNullOrWhiteSpace(instructions))
            {
                return InvokeResult<string>.FromError("Instructions are required.");
            }

            // 1) Embed the instructions
            var embedResult = await _embedder.EmbedAsync(instructions, -1).ConfigureAwait(false);
            if (!embedResult.Successful)
            {
                return InvokeResult<string>.FromError(embedResult.ErrorMessage);
            }

            var vector = embedResult.Result.Vector as float[];
            if (vector == null || vector.Length == 0)
            {
                return InvokeResult<string>.FromError("Embedding vector was empty.");
            }

            // 3) Retrieve candidates from Qdrant
            var searchRequest = new QdrantSearchRequest
            {
                Vector = vector,
                Limit = Math.Clamp(_topK * 3, 12, 50),
                WithPayload = true,
                Filter = filter
            };

            var hits = await _qdrantClient.SearchAsync(
                agentContext.VectorDatabaseCollectionName,
                searchRequest).ConfigureAwait(false);

            _adminLogger.Trace($"[QdrantRagContextBuilder__BuildContextSectionAsync] Query Completed, found {hits.Count} results.");

            if (hits == null || hits.Count == 0)
            {
                // Return an empty but well-formed context block
                return InvokeResult<string>.Create("[CONTEXT]\r\n\r\n");
            }

            // 4) Select a diverse subset
            var selected = SelectDiverse(hits, _topK);
            if (selected.Count == 0)
            {
                return InvokeResult<string>.Create("[CONTEXT]\r\n\r\n");
            }

            // 5) Build the AGN-002 compliant context block
            var contextBlock = await BuildContextBlockAsync(agentContext, selected).ConfigureAwait(false);
            return InvokeResult<string>.Create(contextBlock);
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

                var payload = RagVectorPayload.FromDictionary(h.Payload);

                var path = !string.IsNullOrWhiteSpace(payload.Path)
                    ? payload.Path
                    : (!string.IsNullOrWhiteSpace(payload.FullDocumentBlobUri)
                        ? payload.FullDocumentBlobUri
                        : string.Empty);

                var sym = payload.Symbol ?? string.Empty;
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
        private async Task<string> BuildContextBlockAsync(AgentContext agentContext, IReadOnlyList<QdrantScoredPoint> selected)
        {

            var sb = new StringBuilder();
            sb.AppendLine("[CONTEXT]");
            sb.AppendLine();

            if (selected == null || selected.Count == 0)
            {
                return sb.ToString();
            }

            var chunkIndex = 1;

            foreach (var hit in selected)
            {
                if (hit.Payload == null)
                {
                    _adminLogger.AddError($"[QdrantRagContextBuilder__BuildContextBlockAsync]", "[QdrantRagContextBuilder__BuildContextSectionAsync] Query Completed, did not have have payload.");
                    continue;
                }

                var payload = RagVectorPayload.FromDictionary(hit.Payload);

                _adminLogger.Trace($"[QdrantRagContextBuilder__BuildContextBlockAsync] Processing payload {payload.SemanticId}, path {payload.SourceSliceBlobUri}.");

                var path = !string.IsNullOrWhiteSpace(payload.Path)
                    ? payload.Path
                    : (!string.IsNullOrWhiteSpace(payload.FullDocumentBlobUri)
                        ? payload.FullDocumentBlobUri
                        : string.Empty);

                var startLine = payload.LineStart ?? payload.StartLine ?? 1;
                var endLine = payload.LineEnd ?? payload.EndLine ?? startLine;
                if (endLine < startLine)
                {
                    endLine = startLine;
                }

                var language = !string.IsNullOrWhiteSpace(payload.Language)
                    ? payload.Language
                    : InferLanguageFromPath(path);

                // Resolve blob/file name: prefer BlobUri, then FullDocumentBlobUri, then Path
                var blobName = !string.IsNullOrWhiteSpace(payload.SourceSliceBlobUri)
                    ? payload.SourceSliceBlobUri
                    : (!string.IsNullOrWhiteSpace(payload.FullDocumentBlobUri)
                        ? payload.FullDocumentBlobUri
                        : payload.Path);

                if (string.IsNullOrWhiteSpace(blobName))
                {
                    continue;
                }

                var contentResult = await _contentRepo.GetTextContentAsync(agentContext, blobName).ConfigureAwait(false);
                if (!contentResult.Successful || string.IsNullOrWhiteSpace(contentResult.Result))
                {
                    continue;
                }

                var snippet = SliceLines(contentResult.Result, startLine, endLine);

                // AGN-002 chunk block
                sb.AppendLine("=== CHUNK " + chunkIndex + " ===");
                sb.AppendLine("Id: " + (string.IsNullOrWhiteSpace(payload.SemanticId) ? hit.Id : payload.SemanticId));
                sb.AppendLine("Path: " + (path ?? string.Empty));
                sb.AppendLine("Lines: " + startLine + "-" + endLine);
                sb.AppendLine("Language: " + language);

                Console.WriteLine($"---\r\n{sb.ToString()}\r\n----\r\n\r\n");

                sb.AppendLine("```" + language);
                sb.AppendLine(contentResult.Result);
                sb.AppendLine("```");
                sb.AppendLine();

                chunkIndex++;
            }

            return sb.ToString();
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
