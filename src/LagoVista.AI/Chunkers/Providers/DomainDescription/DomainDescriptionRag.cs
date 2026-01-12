using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.AI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LagoVista.AI.Chunkers.Providers.DomainDescription
{
    /// <summary>
    /// DomainDescriptionRag (IDX-072).
    ///
    /// Concrete IRagDescription implementation for SubtypeKind.DomainDescription.
    /// This class is intentionally deterministic and LLM-free. All text comes from
    /// the domain description document and the domain catalog.
    /// </summary>
    public sealed class DomainDescriptionRag : SummaryFacts, IRagDescription
    {
        /// <summary>
        /// Creates a new domain description.
        /// </summary>
        public DomainDescriptionRag(
            string domainName,
            string domainSummary,
            string domainNarrative,
            IReadOnlyList<ModelClassEntry> classes)
        {
            if (string.IsNullOrWhiteSpace(domainName))
            {
                throw new ArgumentException("Domain name is required.", nameof(domainName));
            }

            DomainName = domainName;
            DomainSummary = domainSummary ?? string.Empty;
            DomainNarrative = domainNarrative ?? string.Empty;
            Classes = classes ?? Array.Empty<ModelClassEntry>();
        }

        /// <summary>
        /// Canonical domain name (for example, "Devices", "Business").
        /// </summary>
        public string DomainName { get; }

        /// <summary>
        /// One-sentence domain summary taken directly from the domain document.
        /// </summary>
        public string DomainSummary { get; }

        /// <summary>
        /// Full domain narrative text taken directly from the domain document.
        /// </summary>
        public string DomainNarrative { get; }

        /// <summary>
        /// Model classes that belong to this domain, as reported by the domain catalog.
        /// </summary>
        public IReadOnlyList<ModelClassEntry> Classes { get; }

        public override string Subtype => SubtypeKind.DomainDescription.ToString();

        /// <summary>
        /// Builds the single SummarySection required for DomainDescription.
        /// </summary>
        public IEnumerable<SummarySection> BuildSummarySections()
        {

            var summarySections = new List<SummarySection>();

            var sectionKey = DomainDescriptionSectionKeyHelper.CreateSectionKey(DomainName);

            var section = new SummarySection
            {
                SectionKey = sectionKey,
                PartIndex = 1,
                PartTotal = 1,
                SymbolType = "Domain",
                SymbolName = DomainName,
                DomainKey = DomainName,
                FinderSnippet = DomainDescriptionSectionBuilder.BuildFinderSnippet(DomainName, DomainSummary),
                BackingArtifact = DomainDescriptionSectionBuilder.BuildBackingArtifact(DomainName, DomainSummary, DomainNarrative, Classes),
            };

            summarySections.Add(section);
            _summarySections = summarySections;

            // NOTE: If SummaryFacts keeps its own internal section list, wire that up here
            // (for example by calling a protected SetSections API). For now we rely on
            // BuildRagPoints() calling this method to obtain the sections.
            return _summarySections;
        }
    }

    /// <summary>
    /// Helper for creating canonical domain SectionKey values.
    /// </summary>
    internal static class DomainDescriptionSectionKeyHelper
    {
        public static string CreateSectionKey(string domainName)
        {
            if (string.IsNullOrWhiteSpace(domainName))
            {
                return "domain-unknown";
            }

            var normalized = NormalizeDomainName(domainName);
            return $"domain-{normalized}";
        }

        /// <summary>
        /// Normalizes a domain name into a stable, lowercase key with hyphens.
        /// </summary>
        public static string NormalizeDomainName(string domainName)
        {
            if (string.IsNullOrWhiteSpace(domainName))
            {
                return "unknown";
            }

            var normalized = domainName.Trim().ToLowerInvariant();
            normalized = normalized.Replace(" ", "-")
                                   .Replace("_", "-");

            while (normalized.Contains("--"))
            {
                normalized = normalized.Replace("--", "-");
            }

            return normalized.Trim('-');
        }
    }

    /// <summary>
    /// Helper for building FinderSnippet and BackingArtifact text for domains.
    /// </summary>
    internal static class DomainDescriptionSectionBuilder
    {
        public static string BuildFinderSnippet(string domainName, string domainSummary)
        {
            domainName = domainName ?? string.Empty;
            domainSummary = domainSummary ?? string.Empty;

            var sb = new StringBuilder();

            sb.Append("Domain: ");
            sb.AppendLine(domainName);
            sb.Append("DomainSummary: ");
            sb.AppendLine(domainSummary);
            sb.AppendLine();
            sb.AppendLine("Kind: Domain");
            sb.AppendLine();
            sb.Append("Artifact: ");
            sb.AppendLine(domainName);
            sb.AppendLine();
            sb.Append("Purpose: Describes the scope and responsibilities of the ");
            sb.Append(domainName);
            sb.AppendLine(" domain.");

            return sb.ToString().TrimEnd();
        }

        public static string BuildBackingArtifact(
            string domainName,
            string domainSummary,
            string domainNarrative,
            IReadOnlyList<ModelClassEntry> classes)
        {
            domainName = domainName ?? string.Empty;
            domainSummary = domainSummary ?? string.Empty;
            domainNarrative = domainNarrative ?? string.Empty;
            classes ??= Array.Empty<ModelClassEntry>();

            var sb = new StringBuilder();

            sb.AppendLine($"# Domain: {domainName}");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine(string.IsNullOrWhiteSpace(domainSummary)
                ? "(no summary provided)"
                : domainSummary);
            sb.AppendLine();
            sb.AppendLine("## Narrative");
            sb.AppendLine(string.IsNullOrWhiteSpace(domainNarrative)
                ? "(no narrative provided)"
                : domainNarrative);
            sb.AppendLine();
            sb.AppendLine("## Model Classes in This Domain");
            sb.AppendLine();

            if (classes.Count == 0)
            {
                sb.AppendLine("(no model classes are currently registered in the domain catalog)");
            }
            else
            {
                foreach (var cls in classes)
                {
                    sb.AppendLine($"### {cls.ClassName}");
                    sb.AppendLine($"- Qualified Name: {cls.QualifiedClassName}");
                    sb.AppendLine($"- Title: {cls.Title}");
                    sb.AppendLine($"- Description: {cls.Description}");
                    sb.AppendLine($"- Help: {cls.HelpText}");
                    sb.AppendLine($"- Path: {cls.RelativePath}");
                    sb.AppendLine();
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}
