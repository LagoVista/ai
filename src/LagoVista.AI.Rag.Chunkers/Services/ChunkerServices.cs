using LagoVista.AI.Rag.Chunkers.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    public class ChunkerServices : IChunkerServices
    {
        public ModelStructureDescription BuildStructuredDescriptionForModel(string sourceText, string relativePath, IReadOnlyDictionary<string, string> resources)
        {
            return ModelStructureDescriptionBuilder.FromSource(sourceText, relativePath, resources);
        }

        public ModelMetadataDescription BuildMetadataDescriptionForModel(string sourceText, string relativePath, IReadOnlyDictionary<string, string> resources)
        {
            return ModelMetadataDescriptionBuilder.FromSource(sourceText, relativePath, resources);
        }

        public string BuildSummaryForMethod(MethodSummaryContext ctx)
        {
            return MethodSummaryBuilder.BuildSummary(ctx);
        }

        public IReadOnlyList<DomainSummaryInfo> ExtractDomains(string source, string filePath)
        {
            return DomainDescriptorSummaryExtractor.Extract(source, filePath);
        }

        public IReadOnlyList<SubKindDetectionResult> DetectSubKindsInSourceFile(string sourceText, string relativePath)
        {
            return SubKindDetector.DetectForFile(sourceText, relativePath);
        }

        public Task<TitleDescriptionReviewResult> ReviewTitleAndDescriptionAsync(SummaryObjectKind kind, string symbolName, string title, string description, string llmUrl, string llmApiKey, HttpClient httpClient = null, string model = "gpt-4.1-mini", CancellationToken cancellationToken = default)
        {
            return OpenAiTitleDescriptionReview.ReviewAsync(
                kind,
                symbolName,
                title,
                description,
                llmUrl,
                llmApiKey,
                httpClient,
                model,
                cancellationToken);
        }
    }
}
