using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.Core.Utils.Types.Nuviot.RagIndexing;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// SummarySection projection and chunking logic for <see cref="DdrDescription"/>.
    /// </summary>
    public partial class DdrDescription : ISummarySectionBuilder
    {
        /// <summary>
        /// Build SummarySection instances for this DDR.
        /// Produces one overview section plus detail sections split
        /// according to the provided token budget.
        /// </summary>
        /// <param name="headerInfo">Optional domain/model header information.</param>
        /// <param name="maxTokens">Approximate token budget for each section part.</param>
        public IEnumerable<SummarySection> BuildSections(DomainModelHeaderInformation headerInfo, int maxTokens = 6500)
        {
            if (maxTokens <= 0)
            {
                maxTokens = 6500;
            }

            var results = new List<SummarySection>();
            _summarySections = results;

            // 1. Overview section (single part)
            var overview = BuildOverviewSection(headerInfo);
            results.Add(overview);

            // 2. Detail sections, split by token budget with overlap and sub-heading awareness
            var maxChars = Math.Max(512, maxTokens * 4); // rough char-per-token approximation
            var overlapTokens = Math.Min(256, Math.Max(0, maxTokens / 4));
            var overlapChars = overlapTokens * 4;

            // Collect all detail parts keyed by final SectionKey
            var detailParts = new List<(string SectionKey, string Text)>();

            foreach (var section in Sections ?? Array.Empty<DdrSectionDescription>())
            {
                if (section == null || string.IsNullOrWhiteSpace(section.RawMarkdown))
                {
                    continue;
                }

                // Rename generic "Section" to "Preamble" for clarity
                var heading = section.Heading;
                var sectionKey = section.SectionKey;

                if (string.Equals(heading, "Section", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(sectionKey, "section", StringComparison.OrdinalIgnoreCase))
                {
                    heading = "Preamble";
                    sectionKey = "preamble";
                }

                var baseKey = BuildDetailSectionKey(sectionKey);

                var parts = BuildDetailNormalizedParts(heading, section.RawMarkdown, maxChars, overlapChars)
                    .ToList();

                if (parts.Count == 0)
                {
                    continue;
                }

                foreach (var part in parts)
                {
                    detailParts.Add((baseKey, part));
                }
            }

            // Now assign PartIndex/PartTotal per SectionKey group
            foreach (var group in detailParts.GroupBy(p => p.SectionKey))
            {
                var totalParts = group.Count();
                var index = 1;

                foreach (var part in group)
                {
                    var summary = new SummarySection
                    {
                        SectionKey = group.Key,
                        SectionType = Subtype,
                        SymbolType = "ddr",
                        Symbol = $"{DdrType}-{DdrNumber:000}",
                        Flavor = "Detail",
                        PartIndex = index++,
                        PartTotal = totalParts,
                        DomainKey = overview.DomainKey,
                        ModelClassName = overview.ModelClassName,
                        ModelName = overview.ModelName,
                        SectionNormalizedText = part.Text
                    };

                    results.Add(summary);
                }
            }

            return results;
        }


        private SummarySection BuildOverviewSection(DomainModelHeaderInformation headerInfo)
        {
            var overview = new SummarySection
            {
                SectionKey = BuildOverviewSectionKey(),
                SectionType = Subtype,
                Flavor = "Overview",
                PartIndex = 1,
                PartTotal = 1,
                SymbolType = "ddr",
                Symbol = $"{DdrType}-{DdrNumber:000}",  
                DomainKey = GetDomainKey(headerInfo),
                ModelClassName = GetModelClassName(headerInfo),
                ModelName = GetModelName(headerInfo),
                SectionNormalizedText = BuildOverviewNormalizedText()
            };

            return overview;
        }

        private string BuildOverviewSectionKey()
        {
            if (!string.IsNullOrWhiteSpace(DdrType) && DdrNumber > 0)
            {
                return $"{DdrType}-{DdrNumber:000}-overview";
            }

            return "ddr-overview";
        }

        private string BuildOverviewNormalizedText()
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(DdrType) && DdrNumber > 0)
            {
                sb.AppendLine($"{DdrType}-{DdrNumber:000} {DdrTitle}");
            }
            else if (!string.IsNullOrWhiteSpace(DdrTitle))
            {
                sb.AppendLine(DdrTitle);
            }

            sb.AppendLine($"Date: {IndexDate}");

            if (!string.IsNullOrWhiteSpace(Status))
            {
                sb.Append("Status: ").AppendLine(Status.Trim());
            }

            if (!string.IsNullOrWhiteSpace(HeaderBlock))
            {
                sb.AppendLine();
                sb.AppendLine("Header:");
                sb.AppendLine(HeaderBlock.Trim());
            }

            if (Sections != null && Sections.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Sections:");
                foreach (var sec in Sections)
                {
                    if (sec == null)
                    {
                        continue;
                    }

                    var heading = sec.Heading;
                    if (string.Equals(heading, "Section", StringComparison.OrdinalIgnoreCase))
                    {
                        heading = "Preamble";
                    }

                    if (!string.IsNullOrWhiteSpace(heading))
                    {
                        sb.Append("- ").AppendLine(heading.Trim());
                    }
                }
            }

            return sb.ToString().Trim();
        }

        private IEnumerable<string> BuildDetailNormalizedParts(string sectionHeading, string rawMarkdown, int maxChars, int overlapChars)
        {
            if (string.IsNullOrWhiteSpace(rawMarkdown))
            {
                yield break;
            }

            // Prefer splitting on "###" sub-headings inside each section.
            var blocks = Regex
                .Split(rawMarkdown, "(?=^###\\s+)", RegexOptions.Multiline)
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .ToList();

            if (blocks.Count == 0)
            {
                blocks.Add(rawMarkdown);
            }

            foreach (var block in blocks)
            {
                var normalized = BuildDetailNormalizedTextForBlock(sectionHeading, block);

                if (normalized.Length <= maxChars)
                {
                    yield return normalized;
                }
                else
                {
                    foreach (var slice in SplitWithOverlap(normalized, maxChars, overlapChars))
                    {
                        yield return slice;
                    }
                }
            }
        }

        private string BuildDetailNormalizedTextForBlock(string sectionHeading, string blockMarkdown)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(DdrType) && DdrNumber > 0)
            {
                sb.AppendLine($"{DdrType}-{DdrNumber:000} {DdrTitle}");
            }
            else if (!string.IsNullOrWhiteSpace(DdrTitle))
            {
                sb.AppendLine(DdrTitle);
            }

            sb.AppendLine($"Date: {IndexDate}");

            if (!string.IsNullOrWhiteSpace(Status))
            {
                sb.Append("Status: ").AppendLine(Status.Trim());
            }


            if (!string.IsNullOrWhiteSpace(sectionHeading))
            {
                sb.Append("Section: ").AppendLine(sectionHeading.Trim());
            }

            // If the block begins with a ### heading, surface it as a subsection label.
            using (var reader = new StringReader(blockMarkdown))
            {
                var firstLine = reader.ReadLine();
                if (!string.IsNullOrWhiteSpace(firstLine) && firstLine.StartsWith("### "))
                {
                    var subHeading = firstLine.Substring(4).Trim();
                    if (!string.IsNullOrWhiteSpace(subHeading))
                    {
                        sb.Append("Subsection: ").AppendLine(subHeading);
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine(blockMarkdown.Trim());

            return sb.ToString().Trim();
        }

        private static IEnumerable<string> SplitWithOverlap(string text, int maxChars, int overlapChars)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            if (maxChars <= 0)
            {
                yield return text;
                yield break;
            }

            var length = text.Length;

            if (length <= maxChars)
            {
                yield return text;
                yield break;
            }

            if (overlapChars < 0)
            {
                overlapChars = 0;
            }

            var start = 0;
            while (start < length)
            {
                var remaining = length - start;
                var take = Math.Min(maxChars, remaining);
                var slice = text.Substring(start, take);
                yield return slice;

                if (remaining <= maxChars)
                {
                    yield break;
                }

                var advance = maxChars - overlapChars;
                if (advance <= 0)
                {
                    // avoid infinite loop; fall back to non-overlapping if configuration is invalid
                    advance = maxChars;
                }

                start += advance;
            }
        }

        private string BuildDetailSectionKey(string sectionKeySuffix)
        {
            var suffix = string.IsNullOrWhiteSpace(sectionKeySuffix) ? "section" : sectionKeySuffix;

            if (!string.IsNullOrWhiteSpace(DdrType) && DdrNumber > 0)
            {
                return $"{DdrType}-{DdrNumber:000}-{suffix}";
            }

            return suffix;
        }

        private static string GetDomainKey(DomainModelHeaderInformation header)
        {
            if (header == null)
            {
                return null;
            }

            var prop = header.GetType().GetProperty("DomainKey");
            return prop?.GetValue(header) as string;
        }

        private static string GetModelClassName(DomainModelHeaderInformation header)
        {
            if (header == null)
            {
                return null;
            }

            var prop = header.GetType().GetProperty("ModelClassName");
            return prop?.GetValue(header) as string;
        }

        private static string GetModelName(DomainModelHeaderInformation header)
        {
            if (header == null)
            {
                return null;
            }

            var prop = header.GetType().GetProperty("ModelName");
            return prop?.GetValue(header) as string;
        }
    }
}
