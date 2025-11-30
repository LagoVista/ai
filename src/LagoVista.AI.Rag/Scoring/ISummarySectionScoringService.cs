namespace LagoVista.AI.Rag.Scoring
{
    /// <summary>
    /// Deterministic scoring engine for semantic summary sections prior to
    /// embedding, as defined by DDR IDX-065.
    /// </summary>
    public interface ISummarySectionScoringService
    {
        /// <summary>
        /// Scores the supplied summary section and returns a
        /// SummarySectionScoreResult containing dimension scores,
        /// composite score, flags, reasons, and any matched domain models.
        /// </summary>
        SummarySectionScoreResult Score(SummarySectionScoreRequest request);
    }
}
