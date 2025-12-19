using System;

namespace LagoVista.AI.Rag.Scoring
{
    /// <summary>
    /// Subtype classification for semantic summaries being scored.
    /// </summary>
    public enum SummarySectionSubtypeKind
    {
        SummarySection = 0,
        NormalizedChunk = 1,
        DDRSection = 2,
        RagSnippet = 3,
        FreeText = 4
    }

    /// <summary>
    /// Scoring dimensions evaluated by the SummarySectionScoringService.
    /// </summary>
    public enum ScoreDimension
    {
        StructuralClarity,
        SemanticCohesion,
        DomainAnchoring,
        NoiseRatio,
        Coverage,
        QueryAlignment
    }

    /// <summary>
    /// High-level quality category for a scored summary section.
    /// </summary>
    public enum SummarySectionScoreCategory
    {
        Excellent,
        Good,
        Fair,
        Poor,
        Reject
    }
}
