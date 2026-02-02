using LagoVista.AI.Chunkers.Providers.DomainDescription;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.Models.UIMetaData; // DomainDescription
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LagoVista.AI.Chunkers.Providers.Domains
{
    /// <summary>
    /// Represents the essential information for a domain that we will index.
    ///
    /// This object is populated by a Roslyn-based extractor and is responsible
    /// for normalizing the text into a <see cref="SummarySection"/>.
    /// </summary>
    public sealed partial class DomainDescription : IDescriptionProvider
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
        public Core.Models.UIMetaData.DomainDescription.DomainTypes DomainType { get; }

        /// <summary>
        /// Fully-qualified type name of the descriptor class.
        /// </summary>
        public string SourceTypeName { get; }

        /// <summary>
        /// Static property name that produced this description.
        /// </summary>
        public string SourcePropertyName { get; }

        public IReadOnlyList<Cluster> Clusters { get; }

        public DomainDescription(
            string domainKey,
            string domainKeyName,
            string title,
            string description,
            Core.Models.UIMetaData.DomainDescription.DomainTypes domainType,
            string sourceTypeName,
            string sourcePropertyName,
            IReadOnlyList<Cluster> clusters )
        {
            DomainKey = domainKey ?? throw new ArgumentNullException(nameof(domainKey));
            DomainKeyName = domainKeyName;
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Description = description ?? string.Empty;
            DomainType = domainType;
            SourceTypeName = sourceTypeName ?? throw new ArgumentNullException(nameof(sourceTypeName));
            SourcePropertyName = sourcePropertyName ?? throw new ArgumentNullException(nameof(sourcePropertyName));
            Clusters = clusters;
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

        /// <summary>
        /// Builds embedding text for a domain record. This is intended for vector indexing (finder snippet),
        /// not for human documentation. Keep it compact, stable, and capability/discovery oriented.
        /// </summary>
        public string BuildFinderSnippet(int maxClusters = 25, int maxDescriptionChars = 500)
        {
            var domain = this;
            var sb = new StringBuilder();

            // Anchor tokens (stable headings help embeddings)
            sb.AppendLine($"domain {NormalizeToken(domain.Title)}");
            sb.AppendLine($"domainKey {NormalizeToken(domain.DomainKey)}");

            if (!string.IsNullOrWhiteSpace(domain.DomainKeyName))
                sb.AppendLine($"domainKeyName {NormalizeToken(domain.DomainKeyName)}");

            sb.AppendLine($"domainType {domain.DomainType}");

            // Clusters: include key + name; description only if short and non-empty.
            var clusters = (domain.Clusters ?? Array.Empty<Cluster>())
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Key))
                .GroupBy(c => c.Key.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    // Merge duplicates: keep first non-empty name/description
                    var first = g.First();
                    var key = first.Key?.Trim();
                    var name = g.Select(x => x.Name?.Trim()).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
                    var desc = g.Select(x => x.Description?.Trim()).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
                    return new Cluster { Key = key, Name = name, Description = desc };
                })
                .OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(0, maxClusters))
                .ToList();

            if (clusters.Count > 0)
            {
                sb.AppendLine("clusters:");
                foreach (var c in clusters)
                {
                    // Keep each line compact.
                    // Example: "agent | Agent | agent configuration and context"
                    var key = NormalizeToken(c.Key);
                    var name = NormalizeToken(c.Name);

                    var line = new StringBuilder();
                    line.Append("- ");
                    line.Append(key);

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        line.Append(" | ");
                        line.Append(name);
                    }

                    var shortDesc = Shorten(c.Description, 140);
                    if (!string.IsNullOrWhiteSpace(shortDesc))
                    {
                        line.Append(" | ");
                        line.Append(NormalizeText(shortDesc));
                    }

                    sb.AppendLine(line.ToString());
                }
            }

            // Description: include, but cap it so domains don’t become huge.
            var descTrimmed = (domain.Description ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(descTrimmed))
            {
                sb.AppendLine("description:");
                sb.AppendLine(NormalizeText(Shorten(descTrimmed, maxDescriptionChars)));
            }

            // Optional: include source type/property as a weak anchor (helps when searching by constant names)
            if (!string.IsNullOrWhiteSpace(domain.SourceTypeName) && !string.IsNullOrWhiteSpace(domain.SourcePropertyName))
                sb.AppendLine($"source {NormalizeToken(domain.SourceTypeName)}.{NormalizeToken(domain.SourcePropertyName)}");

            return sb.ToString().TrimEnd();
        }

        private static string Shorten(string s, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            s = s.Trim();
            if (maxChars <= 0) return string.Empty;
            if (s.Length <= maxChars) return s;
            return s.Substring(0, maxChars).TrimEnd();
        }

        private static string NormalizeToken(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            // Keep tokens compact and consistent for embeddings.
            return NormalizeText(s).Replace('\n', ' ').Replace('\r', ' ').Trim();
        }

        private static string NormalizeText(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            // Minimal normalization: collapse whitespace.
            var sb = new StringBuilder(s.Length);
            bool prevWs = false;

            foreach (var ch in s)
            {
                var isWs = char.IsWhiteSpace(ch);
                if (isWs)
                {
                    if (!prevWs) sb.Append(' ');
                    prevWs = true;
                }
                else
                {
                    sb.Append(ch);
                    prevWs = false;
                }
            }

            return sb.ToString().Trim();
        }

        public string BuildModelSummaryCard()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Domain Title: {Title}");
            sb.AppendLine($"Domain Key: {DomainKey}");
            sb.AppendLine($"Domain Type: {DomainType}");
            sb.AppendLine($"Source: {SourceTypeName}.{SourcePropertyName}");
            sb.AppendLine();
            sb.AppendLine("Domain Description:");
            sb.AppendLine(Description?.Trim() ?? string.Empty);
            sb.AppendLine("Clusters:");
            foreach (var c in Clusters ?? Array.Empty<Cluster>())
                sb.AppendLine($"- {c.Key}: {c.Name}");

            return sb.ToString();
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
            sb.AppendLine("Clusters:");
            foreach (var c in Clusters ?? Array.Empty<Cluster>())
                sb.AppendLine($"- {c.Key}: {c.Name}");

            return new List<SummarySection>
            {
                new SummarySection
                {
                    SectionKey = $"domain-{(DomainKey ?? Title ?? SourcePropertyName).Replace(" ", "-").ToLowerInvariant()}",
                    SymbolName = SourceTypeName,                  // e.g. LagoVista.AI.Models.AIDomain
                    SymbolType = "Domain",                    // your logical kind
                    SectionNormalizedText = BuildModelSummaryCard()
                }
            };
        }

    }
}
