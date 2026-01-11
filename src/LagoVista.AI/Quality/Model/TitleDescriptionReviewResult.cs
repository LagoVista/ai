using System.Collections.Generic;

namespace LagoVista.AI.Quality.Model
{
    /// <summary>
    /// Result of running title/description through the quality service.
    /// Contains suggestions plus a list of issues found.
    /// </summary>
    public sealed class TitleDescriptionReviewResult
    {
        /// <summary>
        /// High-level symbol type, e.g. \"Domain\" or \"Model\".
        /// </summary>
        public string SymbolType { get; set; }

        public string OriginalTitle { get; set; }
        public string OriginalDescription { get; set; }

        public string SuggestedTitle { get; set; }
        public string SuggestedDescription { get; set; }

        /// <summary>
        /// Issues or comments about the title (e.g., spelling, clarity, length).
        /// </summary>
        public List<string> TitleIssues { get; set; } = new List<string>();

        /// <summary>
        /// Issues or comments about the description (e.g., vague, grammar, missing details).
        /// </summary>
        public List<string> DescriptionIssues { get; set; } = new List<string>();

        /// <summary>
        /// True if the LLM suggested any change (title or description).
        /// </summary>
        public bool HasChanges =>
            SuggestedTitle != null && SuggestedTitle != OriginalTitle ||
            SuggestedDescription != null && SuggestedDescription != OriginalDescription;
    }
}
