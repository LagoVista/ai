using System.Collections.Generic;

namespace LagoVista.AI.Indexing.Models
{
    /// <summary>
    /// Domain-level result for a single title/description/help refinement operation
    /// (IDX-066). This is the object consumed by the orchestrator and catalog.
    ///
    /// NOTE: The HttpLlmTitleDescriptionClient also uses this type as its structured
    /// output carrier; the service adds context (kind, symbol, originals, errors).
    /// </summary>
    public class TitleDescriptionReviewResult
    {
        /// <summary>
        /// Kind of object being refined (model vs domain).
        /// </summary>
        public SummaryObjectKind Kind { get; set; }

        /// <summary>
        /// Class name for models, or domain symbol for domains.
        /// </summary>
        public string SymbolName { get; set; }

        /// <summary>
        /// Original title value before refinement.
        /// </summary>
        public string OriginalTitle { get; set; }

        /// <summary>
        /// Original description value before refinement.
        /// </summary>
        public string OriginalDescription { get; set; }

        /// <summary>
        /// Original help value before refinement (may be null).
        /// </summary>
        public string OriginalHelp { get; set; }

        /// <summary>
        /// Refined title returned by the LLM (may be identical to original).
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Refined description returned by the LLM (may be identical to original).
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Refined help/tooltip text (may be null).
        /// </summary>
        public string Help { get; set; }

        /// <summary>
        /// True if any of title/description/help differ from the original values
        /// after guard-rail processing.
        /// </summary>
        public bool HasChanges { get; set; }

        /// <summary>
        /// True if this item should be explicitly reviewed by a human (for example,
        /// due to LLM uncertainty, structural issues, or catalog warnings).
        /// </summary>
        public bool RequiresAttention { get; set; }

        /// <summary>
        /// Warnings and questions associated with this refinement. This includes both
        /// LLM-provided warnings and guard-rail / error annotations from the service.
        /// </summary>
        public List<string> Warnings { get; } = new List<string>();

        /// <summary>
        /// True if the refinement encountered a hard error (for example, HTTP failure
        /// or malformed structured output). The orchestrator uses this to distinguish
        /// between "no changes" and "could not process".
        /// </summary>
        public bool IsError { get; set; }

        /// <summary>
        /// Optional error message associated with <see cref="IsError"/>.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Convenience flag for orchestrator/catalog logic.
        /// </summary>
        public bool IsSuccessful => !IsError;

        /// <summary>
        /// Human-readable reason for failure (when <see cref="IsSuccessful"/> is false).
        /// Typically derived from <see cref="ErrorMessage"/>.
        /// </summary>
        public string FailureReason { get; set; }

        /// <summary>
        /// Optional notes associated with this refinement (for example, a concatenation
        /// of warnings) used by the catalog for ReasonOrNotes.
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// Convenience aliases used by the orchestrator and catalog.
        /// </summary>
        public string RefinedTitle => Title;
        public string RefinedDescription => Description;
        public string RefinedHelp => Help;

        /// <summary>
        /// Factory for creating an error result while preserving the original values.
        /// The orchestrator can still index the original text but will see that this
        /// item requires attention.
        /// </summary>
        public static TitleDescriptionReviewResult FromError(
            SummaryObjectKind kind,
            string symbolName,
            string originalTitle,
            string originalDescription,
            string originalHelp,
            string errorMessage)
        {
            var result = new TitleDescriptionReviewResult
            {
                Kind = kind,
                SymbolName = symbolName,
                OriginalTitle = originalTitle,
                OriginalDescription = originalDescription,
                OriginalHelp = originalHelp,
                Title = originalTitle,
                Description = originalDescription,
                Help = originalHelp,
                HasChanges = false,
                RequiresAttention = true,
                IsError = true,
                ErrorMessage = errorMessage,
                FailureReason = errorMessage
            };

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                var warning = $"LLM error: {errorMessage}";
                result.Warnings.Add(warning);
                result.Notes = warning;
            }

            return result;
        }
    }
}
