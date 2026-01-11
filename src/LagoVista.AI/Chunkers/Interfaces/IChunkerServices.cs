using LagoVista.AI.Rag.Chunkers.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using LagoVista.Core.Utils.Types;
using LagoVista.Core.Validation;
using LagoVista.AI.Chunkers.Providers.DomainDescription;
using LagoVista.AI.Quality.Model;

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

        string BuildSummaryForMethod(MethodSummaryContext ctx);

        IReadOnlyList<DomainSummaryInfo> ExtractDomains(string filePath);

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
