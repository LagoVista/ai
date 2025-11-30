using System;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Scoring
{
    /// <summary>
    /// Request to score a semantic summary section prior to embedding.
    /// </summary>
    public sealed class SummarySectionScoreRequest
    {
        /// <summary>
        /// Stable identifier for this snippet within the indexing run.
        /// </summary>
        public string SnippetId { get; set; }

        /// <summary>
        /// The semantic text that will be embedded if approved.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Classification of the snippet subtype for reporting and policy.
        /// </summary>
        public SummarySectionSubtypeKind SubtypeKind { get; set; }

        /// <summary>
        /// Optional metadata (e.g. file path, repo, line range) used only for
        /// reporting and diagnostics.
        /// </summary>
        public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Information about a domain model whose name was detected in the text.
    /// </summary>
    public sealed class MatchedModelInfo
    {
        public string Name { get; set; }
        public string Domain { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Deterministic scoring result from the SummarySectionScoringService.
    /// </summary>
    public sealed class SummarySectionScoreResult
    {
        public string SnippetId { get; set; }
        public SummarySectionSubtypeKind SubtypeKind { get; set; }

        /// <summary>
        /// Composite quality score in the range [0,100].
        /// </summary>
        public double CompositeScore { get; set; }

        public SummarySectionScoreCategory Category { get; set; }

        /// <summary>
        /// Per-dimension scores, each in the range [0,100].
        /// </summary>
        public IDictionary<ScoreDimension, double> DimensionScores { get; set; } =
            new Dictionary<ScoreDimension, double>();

        /// <summary>
        /// Machine-readable flags indicating reasons for penalties, e.g.
        /// "LowDomainAnchoring", "HighNoise", etc.
        /// </summary>
        public IList<string> Flags { get; set; } = new List<string>();

        /// <summary>
        /// Human-readable explanations for the score to support reports
        /// and future rewrite flows.
        /// </summary>
        public IList<string> Reasons { get; set; } = new List<string>();

        /// <summary>
        /// Any domain models detected in the text, based on the supplied
        /// GlobalModelDescriptor list.
        /// </summary>
        public IList<MatchedModelInfo> MatchedModels { get; set; } = new List<MatchedModelInfo>();
    }

    /// <summary>
    /// Final handling result from the SummarySectionScoreHandler, which acts
    /// as the publish/suppress gate for embedding.
    /// </summary>
    public sealed class SummarySectionScoreHandlingResult
    {
        public string SnippetId { get; set; }

        /// <summary>
        /// The text that should ultimately be embedded if ShouldPublish is true.
        /// This may be identical to the original text or a rewritten variant.
        /// </summary>
        public string FinalText { get; set; }

        /// <summary>
        /// The final composite score associated with FinalText.
        /// </summary>
        public double FinalCompositeScore { get; set; }

        /// <summary>
        /// True if the snippet is allowed to be embedded; false if it should
        /// be suppressed.
        /// </summary>
        public bool ShouldPublish { get; set; }

        /// <summary>
        /// High-level disposition string, e.g. "Accepted", "AcceptedAfterRewrite",
        /// "RejectedLowScore".
        /// </summary>
        public string Disposition { get; set; }

        /// <summary>
        /// Number of rewrite attempts performed by the handler. For the initial
        /// implementation this will typically be zero.
        /// </summary>
        public int RewriteCount { get; set; }

        /// <summary>
        /// Final reasons explaining why the snippet was accepted or rejected.
        /// </summary>
        public IList<string> Reasons { get; set; } = new List<string>();
    }
}
