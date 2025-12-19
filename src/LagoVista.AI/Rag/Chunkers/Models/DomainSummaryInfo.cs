using System;
using System.Collections.Generic;
using System.Text;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.Core.Models.UIMetaData; // DomainDescription

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// Represents the essential information for a domain that we will index.
    ///
    /// This object is populated by a Roslyn-based extractor and is responsible
    /// for normalizing the text into a <see cref="SummarySection"/>.
    /// </summary>
    public sealed class DomainSummaryInfo : ISummarySectionBuilder
    {
        /// <summary>
        /// The key used by the DomainDescription attribute (e.g. "AI Admin").
        /// </summary>
        public string DomainKey { get; }

        /// <summary>
        /// Emtities refer to the domain key by a constant, when we are statically parsing each
        /// entity, we only have the name of the constant, capture that here so we can do a lookup.
        /// </summary>
        public string DomainKeyName { get; set; }

        /// <summary>
        /// User-facing domain title/name (e.g. "AI Admin").
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Human-readable description of the domain.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Domain type enum value from <see cref="DomainDescription.DomainTypes"/>.
        /// </summary>
        public DomainDescription.DomainTypes DomainType { get; }

        /// <summary>
        /// Fully-qualified type name of the descriptor class.
        /// </summary>
        public string SourceTypeName { get; }

        /// <summary>
        /// Static property name that produced this description.
        /// </summary>
        public string SourcePropertyName { get; }

        public DomainSummaryInfo(
            string domainKey,
            string domainKeyName,
            string title,
            string description,
            DomainDescription.DomainTypes domainType,
            string sourceTypeName,
            string sourcePropertyName)
        {
            DomainKey = domainKey ?? throw new ArgumentNullException(nameof(domainKey));
            DomainKeyName = domainKeyName;
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Description = description ?? string.Empty;
            DomainType = domainType;
            SourceTypeName = sourceTypeName ?? throw new ArgumentNullException(nameof(sourceTypeName));
            SourcePropertyName = sourcePropertyName ?? throw new ArgumentNullException(nameof(sourcePropertyName));
        }

        /// <summary>
        /// Cheap structural validation — spelling/grammar checks can be layered
        /// on top of this using an LLM-based quality service.
        /// </summary>
        public IReadOnlyList<string> ValidateBasicQuality()
        {
            var issues = new List<string>();

            if (string.IsNullOrWhiteSpace(Title))
                issues.Add("Domain title is empty.");

            if (string.IsNullOrWhiteSpace(Description))
                issues.Add($"Domain '{Title}' has an empty description.");

            if (!string.IsNullOrWhiteSpace(Description) && Description.Length < 20)
                issues.Add($"Domain '{Title}' description is very short; consider expanding it.");

            if (!string.IsNullOrWhiteSpace(Title) && Title.Length < 3)
                issues.Add($"Domain '{Title}' title is very short; consider a more descriptive name.");

            return issues;
        }

        public IEnumerable<SummarySection> BuildSections(DomainModelHeaderInformation headerInfo, int maxTokens = 6500)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Domain Title: {Title}");
            sb.AppendLine($"Domain Key: {DomainKey}");
            sb.AppendLine($"Domain Type: {DomainType}");
            sb.AppendLine($"Source: {SourceTypeName}.{SourcePropertyName}");
            sb.AppendLine();
            sb.AppendLine("Domain Description:");
            sb.AppendLine(Description?.Trim() ?? string.Empty);

            return new List<SummarySection>
            {
                new SummarySection
                {
                    SectionKey = $"domain-{(DomainKey ?? Title ?? SourcePropertyName).Replace(" ", "-").ToLowerInvariant()}",
                    Symbol = SourceTypeName,                  // e.g. LagoVista.AI.Models.AIDomain
                    SymbolType = "Domain",                    // your logical kind
                    SectionNormalizedText = sb.ToString()
                }
            };
        }

    }
}
