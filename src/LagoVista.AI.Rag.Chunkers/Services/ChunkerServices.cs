using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.Utils.Types;
using LagoVista.Core.Validation;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    public class ChunkerServices : IChunkerServices
    {
        public ModelStructureDescription BuildStructuredDescriptionForModel(string sourceText, IReadOnlyDictionary<string, string> resources)
        {
            return ModelStructureDescriptionBuilder.FromSource(sourceText, resources);
        }

        public ModelMetadataDescription BuildMetadataDescriptionForModel(string sourceText, IReadOnlyDictionary<string, string> resources)
        {
            return ModelMetadataDescriptionBuilder.FromSource(sourceText, resources);
        }

        public string BuildSummaryForMethod(MethodSummaryContext ctx)
        {
            return MethodSummaryBuilder.BuildSummary(ctx);
        }

        public IReadOnlyList<DomainSummaryInfo> ExtractDomains(string source)
        {
            return DomainDescriptorSummaryExtractor.Extract(source);
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

        public SourceKindResult DetectForFile(string sourceText, string relativePath)
        {
            return SourceKindAnalyzer.AnalyzeFile(sourceText, relativePath);
        }

        public int EstimateTokens(string s)
        {
            return TokenEstimator.EstimateTokens(s);
        }

        public  InvokeResult<RagChunkPlan> ChunkCSharpWithRoslyn(string text, string fileName, int maxTokensPerChunk = 6500, int overlapLines = 6)
        {
            return RoslynCSharpChunker.Chunk(text, fileName, maxTokensPerChunk, overlapLines);
        }
    }
}
