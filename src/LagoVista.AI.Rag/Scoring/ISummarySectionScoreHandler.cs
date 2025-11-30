namespace LagoVista.AI.Rag.Scoring
{
    /// <summary>
    /// Publication gate for scored summary sections. Receives the deterministic
    /// score result and returns a final handling result indicating whether
    /// the snippet should be published (embedded) or suppressed.
    /// </summary>
    public interface ISummarySectionScoreHandler
    {
        /// <summary>
        /// Handle a scored summary section and return the final handling
        /// decision, including publish flag, disposition, and (optionally)
        /// rewritten text.
        /// </summary>
        SummarySectionScoreHandlingResult Handle(
            SummarySectionScoreRequest request,
            SummarySectionScoreResult scoreResult);
    }

    /// <summary>
    /// Factory for obtaining a score handler for a given summary section
    /// subtype. In the initial implementation this may return a single
    /// handler instance, but it provides flexibility for CI/CD or repo-specific
    /// policies.
    /// </summary>
    public interface ISummarySectionScoreHandlerFactory
    {
        ISummarySectionScoreHandler CreateHandler(SummarySectionSubtypeKind subtypeKind);
    }
}
