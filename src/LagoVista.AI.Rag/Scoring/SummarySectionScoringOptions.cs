using System.Collections.Generic;

namespace LagoVista.AI.Rag.Scoring
{
    /// <summary>
    /// Tunable configuration options for the SummarySectionScoringService.
    /// </summary>
    public sealed class SummarySectionScoringOptions
    {
        /// <summary>
        /// Weight for the structural clarity dimension in the composite score.
        /// </summary>
        public double StructuralClarityWeight { get; set; } = 0.25;

        /// <summary>
        /// Weight for the semantic cohesion dimension in the composite score.
        /// </summary>
        public double SemanticCohesionWeight { get; set; } = 0.25;

        /// <summary>
        /// Weight for the domain anchoring dimension in the composite score.
        /// </summary>
        public double DomainAnchoringWeight { get; set; } = 0.20;

        /// <summary>
        /// Weight for the noise ratio dimension in the composite score.
        /// </summary>
        public double NoiseRatioWeight { get; set; } = 0.10;

        /// <summary>
        /// Weight for the coverage dimension in the composite score.
        /// </summary>
        public double CoverageWeight { get; set; } = 0.10;

        /// <summary>
        /// Weight for the query alignment dimension in the composite score.
        /// </summary>
        public double QueryAlignmentWeight { get; set; } = 0.10;

        /// <summary>
        /// Optional list of domain-intent verbs (e.g. "provision", "register",
        /// "embed", "index") used to boost coverage and query alignment
        /// scores when present.
        /// </summary>
        public IList<string> DomainVerbs { get; set; } = new List<string>();

        /// <summary>
        /// Optional list of role keywords (e.g. "manager", "service", "tool",
        /// "primitive") used to identify alignment with expected query patterns.
        /// </summary>
        public IList<string> RoleKeywords { get; set; } = new List<string>();
    }

    /// <summary>
    /// Options controlling how the SummarySectionScoreHandler makes
    /// publish/suppress decisions.
    /// </summary>
    public sealed class SummarySectionScoreHandlerOptions
    {
        /// <summary>
        /// Minimum composite score required for a snippet to be published
        /// without being rejected. Defaults to 60.
        /// </summary>
        public double MinPublishScore { get; set; } = 60.0;
    }
}
