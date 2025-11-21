using LagoVista.AI.Rag.Chunkers.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using LagoVista.Core.Utils.Types;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// Top-level contract for chunking and model/domain extraction services.
    /// Implementations should delegate to specialized static helpers.
    /// </summary>
    public interface IChunkerServices
    {
        IReadOnlyList<SubKindDetectionResult> DetectForFile(string sourceText, string relativePath);

        int EstimateTokens(string s);

        RagChunkPlan ChunkCSharpWithRoslyn(
            string text,
            string relPath,
            string blobPath,
            int maxTokensPerChunk = 6500,
            int overlapLines = 6);

        ModelMetadataDescription BuildMetadataDescriptionForModel(
            string sourceText,
            string relativePath,
            IReadOnlyDictionary<string, string> resources);

        ModelStructureDescription BuildStructuredDescriptionForModel(
            string sourceText,
            string relativePath,
            IReadOnlyDictionary<string, string> resources);

        string BuildSummaryForMethod(MethodSummaryContext ctx);

        IReadOnlyList<DomainSummaryInfo> ExtractDomains(
            string source,
            string filePath);

        string BuildModelSummary(ModelMetadataDescription metadata)
        {
            return ModelMetadataSummaryBuilder.BuildSummary(metadata);
        }

        Task<TitleDescriptionReviewResult> ReviewTitleAndDescriptionAsync(
            SummaryObjectKind kind,
            string symbolName,
            string title,
            string description,
            string llmUrl,
            string llmApiKey,
            HttpClient httpClient = null,
            string model = "gpt-4.1-mini",
            CancellationToken cancellationToken = default);
    }
}
