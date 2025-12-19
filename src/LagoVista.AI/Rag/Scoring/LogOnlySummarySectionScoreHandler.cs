using System;
using System.Collections.Generic;
using LagoVista.Core.PlatformSupport;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Rag.Scoring
{
    /// <summary>
    /// Default score handler implementation which logs low-scoring summaries
    /// and acts as the publish/suppress gate based on a minimum score
    /// threshold. No rewrites are performed in this initial implementation.
    /// </summary>
    public sealed class LogOnlySummarySectionScoreHandler : ISummarySectionScoreHandler
    {
        private readonly IAdminLogger _logger;
        private readonly SummarySectionScoreHandlerOptions _options;

        public LogOnlySummarySectionScoreHandler(
            IAdminLogger logger,
            SummarySectionScoreHandlerOptions options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? new SummarySectionScoreHandlerOptions();
        }

        public SummarySectionScoreHandlingResult Handle(
            SummarySectionScoreRequest request,
            SummarySectionScoreResult scoreResult)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (scoreResult == null) throw new ArgumentNullException(nameof(scoreResult));

            var result = new SummarySectionScoreHandlingResult
            {
                SnippetId = request.SnippetId,
                FinalText = request.Text,
                FinalCompositeScore = scoreResult.CompositeScore,
                RewriteCount = 0,
                Reasons = new List<string>()
            };

            var shouldPublish = scoreResult.CompositeScore >= _options.MinPublishScore;
            result.ShouldPublish = shouldPublish;
            result.Disposition = shouldPublish ? "Accepted" : "RejectedLowScore";

            foreach (var reason in scoreResult.Reasons)
            {
                result.Reasons.Add(reason);
            }

            if (!shouldPublish)
            {
                var properties = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("SnippetId", request.SnippetId ?? string.Empty),
                    new KeyValuePair<string, string>("SubtypeKind", scoreResult.SubtypeKind.ToString()),
                    new KeyValuePair<string, string>("CompositeScore", scoreResult.CompositeScore.ToString("F2")),
                    new KeyValuePair<string, string>("Category", scoreResult.Category.ToString()),
                    new KeyValuePair<string, string>("Flags", string.Join(",", scoreResult.Flags ?? new List<string>()))
                };

                _logger.AddCustomEvent(
                    LogLevel.Warning,
                    "SummarySectionScoreHandler.LowScore",
                    "Summary section scored below publish threshold.",
                    properties.ToArray());
            }

            return result;
        }
    }

    /// <summary>
    /// Simple factory that currently returns a single handler implementation
    /// for all subtypes, but allows subtype-specific handlers in the future.
    /// </summary>
    public sealed class SummarySectionScoreHandlerFactory : ISummarySectionScoreHandlerFactory
    {
        private readonly ISummarySectionScoreHandler _handler;

        public SummarySectionScoreHandlerFactory(ISummarySectionScoreHandler handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public ISummarySectionScoreHandler CreateHandler(SummarySectionSubtypeKind subtypeKind)
        {
            // For now we ignore subtypeKind and return a single handler.
            return _handler;
        }
    }
}
