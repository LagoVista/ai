// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: TBD
// IndexVersion: 1
// --- END CODE INDEX META ---
using LagoVista.AI.Rag.Chunkers.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LagoVista.AI.Rag.Chunkers.Services
{

    /// <summary>
    /// Helper for producing simple, human-readable summary sentences and header
    /// lines for methods based on domain/model/SubKind context.
    ///
    /// For now this is intentionally conservative and serves as a placeholder. It
    /// gives us a single place to evolve the text we embed in SummarySections and
    /// Roslyn chunk headers without touching all call sites.
    /// </summary>
    public static class MethodSummaryBuilder
    {
        /// <summary>
        /// Builds a single summary sentence suitable for inclusion in a
        /// SummarySection.SectionNormalizedText.
        /// </summary>
        public static string BuildSummary(MethodSummaryContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(ctx.MethodName))
            {
                parts.Add($"Method {ctx.MethodName}");
            }

            if (!string.IsNullOrWhiteSpace(ctx.SubKind))
            {
                parts.Add($"({ctx.SubKind})");
            }

            if (!string.IsNullOrWhiteSpace(ctx.ModelName))
            {
                parts.Add($"operates on the {ctx.ModelName} model");
            }

            if (!string.IsNullOrWhiteSpace(ctx.DomainName))
            {
                parts.Add($"in the {ctx.DomainName} domain");
            }

            var core = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

            var extras = new List<string>();

            if (!string.IsNullOrWhiteSpace(ctx.DomainTagline))
            {
                extras.Add($"Domain focus: {ctx.DomainTagline}");
            }

            if (!string.IsNullOrWhiteSpace(ctx.ModelTagline))
            {
                extras.Add($"Model focus: {ctx.ModelTagline}");
            }

            if (!string.IsNullOrWhiteSpace(ctx.Signature))
            {
                extras.Add($"Signature: {ctx.Signature}");
            }

            var allSegments = new List<string>();

            if (!string.IsNullOrWhiteSpace(core))
            {
                allSegments.Add(core.Trim());
            }

            if (extras.Count > 0)
            {
                allSegments.Add(string.Join(". ", extras));
            }

            var text = string.Join(". ", allSegments).Trim();

            if (!string.IsNullOrEmpty(text) && !text.EndsWith(".", StringComparison.Ordinal))
            {
                text += ".";
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                text = "Method summary placeholder.";
            }

            return text;
        }

        /// <summary>
        /// Builds a single-line comment header suitable for prepending to a
        /// Roslyn code chunk. This can be used to give the embedder a small,
        /// natural-language hint about what the code does without changing the
        /// code itself.
        /// </summary>
        public static string BuildHeaderComment(MethodSummaryContext ctx)
        {
            var summary = BuildSummary(ctx);
            return string.IsNullOrWhiteSpace(summary)
                ? "// Method summary placeholder."
                : "// " + summary;
        }
    }
}
