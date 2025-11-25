using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Services;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// SummarySection implementation for SummaryDataDescription (IDX-0052).
    ///
    /// Converts the structured list/summary metadata into human-readable
    /// sections suitable for embedding, aligned with the same pattern used
    /// by ModelStructureDescription and other description models.
    ///
    /// For V1 we emit two sections:
    /// - summary-list-overview
    /// - summary-list-fields
    ///
    /// Sections are kept as single blocks; maxTokens is accepted for
    /// contract consistency but not used for intra-section splitting yet.
    /// </summary>
    public sealed partial class SummaryDataDescription : ISummarySectionBuilder
    {
        /// <inheritdoc />
        public IEnumerable<SummarySection> BuildSections(
            DomainModelHeaderInformation headerInfo,
            int maxTokens = 6500)
        {
            if (maxTokens <= 0)
            {
                maxTokens = 6500;
            }

            var sections = new List<SummarySection>();

            // Symbol: prefer logical underlying entity name; fall back to list name.
            var symbol = !string.IsNullOrWhiteSpace(UnderlyingEntityTypeName)
                ? UnderlyingEntityTypeName
                : !string.IsNullOrWhiteSpace(ListName)
                    ? ListName
                    : "(summary-list)";

            // =====================================================================
            // summary-list-overview
            // =====================================================================
            var overview = new StringBuilder();

            var domainLine = BuildDomainLine(headerInfo);
            var modelLine = BuildModelLine(headerInfo);

            if (!string.IsNullOrWhiteSpace(domainLine))
            {
                overview.AppendLine(domainLine);
            }

            if (!string.IsNullOrWhiteSpace(modelLine))
            {
                overview.AppendLine(modelLine);
            }

            if (!string.IsNullOrWhiteSpace(headerInfo?.DomainKey))
            {
                overview.AppendLine($"Domain Key: {headerInfo.DomainKey}");
            }

            if (!string.IsNullOrWhiteSpace(domainLine) ||
                !string.IsNullOrWhiteSpace(modelLine) ||
                !string.IsNullOrWhiteSpace(headerInfo?.DomainKey))
            {
                overview.AppendLine();
            }

            // High-level identity / semantics for the list surface.
            if (!string.IsNullOrWhiteSpace(ListName))
            {
                overview.AppendLine($"List: {ListName}");
            }

            if (!string.IsNullOrWhiteSpace(UnderlyingEntityTypeName))
            {
                overview.AppendLine($"Underlying Entity: {UnderlyingEntityTypeName}");
            }

            if (!string.IsNullOrWhiteSpace(SummaryTypeName))
            {
                overview.AppendLine($"Summary Type: {SummaryTypeName}");
            }

            if (!string.IsNullOrWhiteSpace(QualifiedName) &&
                !string.Equals(QualifiedName, SummaryTypeName, StringComparison.Ordinal))
            {
                overview.AppendLine($"Qualified Name: {QualifiedName}");
            }

            if (!string.IsNullOrWhiteSpace(Domain))
            {
                overview.AppendLine($"Domain Classification: {Domain}");
            }

            if (!string.IsNullOrWhiteSpace(Title))
            {
                overview.AppendLine($"Title: {Title}");
            }

            if (!string.IsNullOrWhiteSpace(Description))
            {
                overview.AppendLine($"Description: {Description.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(Help))
            {
                overview.AppendLine("Help:");
                overview.AppendLine(Help.Trim());
            }

            if (!string.IsNullOrWhiteSpace(BehaviorDescription))
            {
                overview.AppendLine();
                overview.AppendLine("Behavior:");
                overview.AppendLine(BehaviorDescription.Trim());
            }

            // Navigation affordances: list UI and API endpoints.
            var affordanceLines = new List<string>();

            if (!string.IsNullOrWhiteSpace(ListUIUrl)) affordanceLines.Add($"List UI: {ListUIUrl}");
            if (!string.IsNullOrWhiteSpace(GetListUrl)) affordanceLines.Add($"Get List: {GetListUrl}");

            if (affordanceLines.Count > 0)
            {
                overview.AppendLine();
                overview.AppendLine("List Affordances:");
                foreach (var line in affordanceLines)
                {
                    overview.AppendLine("- " + line);
                }
            }

            sections.Add(new SummarySection
            {
                SectionKey = "summary-list-overview",
                SectionType = "Overview",
                Flavor = "SummaryDataDescription",
                Symbol = symbol,
                SymbolType = "SummaryList",
                DomainKey = headerInfo?.DomainKey,
                ModelClassName = headerInfo?.ModelClassName,
                ModelName = headerInfo?.ModelName,
                SectionNormalizedText = overview.ToString().Trim()
            });

            // =====================================================================
            // summary-list-fields
            // =====================================================================
            var fieldsSection = new StringBuilder();

            domainLine = BuildDomainLine(headerInfo);
            modelLine = BuildModelLine(headerInfo);

            if (!string.IsNullOrWhiteSpace(domainLine))
            {
                fieldsSection.AppendLine(domainLine);
            }

            if (!string.IsNullOrWhiteSpace(modelLine))
            {
                fieldsSection.AppendLine(modelLine);
            }

            if (!string.IsNullOrWhiteSpace(headerInfo?.DomainKey))
            {
                fieldsSection.AppendLine($"Domain Key: {headerInfo.DomainKey}");
            }

            if (!string.IsNullOrWhiteSpace(domainLine) ||
                !string.IsNullOrWhiteSpace(modelLine) ||
                !string.IsNullOrWhiteSpace(headerInfo?.DomainKey))
            {
                fieldsSection.AppendLine();
            }

            var symbolForFields = !string.IsNullOrWhiteSpace(ListName) ? ListName : symbol;
            fieldsSection.AppendLine($"Fields for list {symbolForFields}:");

            fieldsSection.AppendLine("  All fields inherited from SummaryData to include Name, Key, Description, IsPublic, IsDeleted, IsDraft, Category, CategoryId, CategoryKey, Stars, RatingCount, LastUpdated");

            var fields = Fields ?? Array.Empty<SummaryDataFieldDescription>();

            if (!fields.Any())
            {
                fieldsSection.AppendLine("  (no additional fields detected)");
            }
            else
            {
                foreach (var field in fields)
                {
                    if (field == null)
                    {
                        continue;
                    }

                    // Line 1: Name : Type
                    fieldsSection.AppendLine($"- {field.Name} : {field.ClrType ?? "(unknown-type)"}");

                    var flags = new List<string>();

                    flags.Add("Visible: " + field.IsVisible);
                    flags.Add("BaseSummaryDataField: " + field.IsBaseSummaryDataField);

                    if (!string.IsNullOrWhiteSpace(field.Header))
                    {
                        flags.Add("Header: " + field.Header);
                    }

                    if (flags.Count > 0)
                    {
                        fieldsSection.AppendLine("  " + string.Join(", ", flags));
                    }
                }
            }

            sections.Add(new SummarySection
            {
                SectionKey = "summary-list-fields",
                SectionType = "Fields",
                Flavor = "SummaryDataDescription",
                Symbol = symbol,
                SymbolType = "SummaryList",
                DomainKey = headerInfo?.DomainKey,
                ModelClassName = headerInfo?.ModelClassName,
                ModelName = headerInfo?.ModelName,
                SectionNormalizedText = fieldsSection.ToString().Trim()
            });

            return sections;
        }

        // ---------------------------------------------------------------------
        // Domain/model header helpers (aligned with ModelStructureDescription)
        // ---------------------------------------------------------------------

        private static string BuildDomainLine(DomainModelHeaderInformation header)
        {
            if (header == null)
            {
                return null;
            }

            var hasName = !string.IsNullOrWhiteSpace(header.DomainName);
            var hasTagline = !string.IsNullOrWhiteSpace(header.DomainTagLine);

            if (!hasName && !hasTagline)
            {
                return null;
            }

            if (hasName && hasTagline)
            {
                return $"Domain: {header.DomainName} — {header.DomainTagLine}";
            }

            if (hasName)
            {
                return $"Domain: {header.DomainName}";
            }

            return header.DomainTagLine;
        }

        private static string BuildModelLine(DomainModelHeaderInformation header)
        {
            if (header == null)
            {
                return null;
            }

            var modelName = !string.IsNullOrWhiteSpace(header.ModelName)
                ? header.ModelName
                : header.ModelClassName;

            var hasName = !string.IsNullOrWhiteSpace(modelName);
            var hasTagline = !string.IsNullOrWhiteSpace(header.ModelTagLine);

            if (!hasName && !hasTagline)
            {
                return null;
            }

            if (hasName && hasTagline)
            {
                return $"Model: {modelName} — {header.ModelTagLine}";
            }

            if (hasName)
            {
                return $"Model: {modelName}";
            }

            return header.ModelTagLine;
        }
    }
}
