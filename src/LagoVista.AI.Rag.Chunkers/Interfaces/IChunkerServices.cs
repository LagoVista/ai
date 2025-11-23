using LagoVista.AI.Rag.Chunkers.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using LagoVista.Core.Utils.Types;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// Top-level contract for chunking and model/domain extraction services.
    /// Implementations should delegate to specialized static helpers.
    /// </summary>
    public interface IChunkerServices
    {
        SourceKindResult DetectForFile(string sourceText, string relativePath);

        int EstimateTokens(string s);

        InvokeResult<IReadOnlyList<CSharpComponentChunk>> ChunkCSharpWithRoslyn(string text, string fileName, int maxTokensPerChunk = 6500, int overlapLines = 6);

        ModelMetadataDescription BuildMetadataDescriptionForModel(string sourceText, IReadOnlyDictionary<string, string> resources);

        ModelStructureDescription BuildStructuredDescriptionForModel(string sourceText, IReadOnlyDictionary<string, string> resources);

        string BuildSummaryForMethod(MethodSummaryContext ctx);

        IReadOnlyList<DomainSummaryInfo> ExtractDomains(string filePath);

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
