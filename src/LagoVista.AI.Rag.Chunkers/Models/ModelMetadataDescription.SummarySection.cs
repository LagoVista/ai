using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// SummarySection implementation for ModelMetadataDescription (IDX-0038).
    /// Converts rich UI/metadata into human-readable text for structured embeddings.
    /// </summary>
    public sealed partial class ModelMetadataDescription : ISummarySectionBuilder
    {
        public IEnumerable<SummarySection> BuildSections(DomainModelHeaderInformation headerInfo, int maxTokens = 6500)
        {
            if (maxTokens <= 0) maxTokens = 6500;

            var sections = new List<SummarySection>();

            var symbol = !string.IsNullOrWhiteSpace(headerInfo?.ModelName)
                ? headerInfo.ModelName
                : !string.IsNullOrWhiteSpace(headerInfo?.ModelClassName)
                    ? headerInfo.ModelClassName
                    : !string.IsNullOrWhiteSpace(ModelName)
                        ? ModelName
                        : "(unknown-model)";

            // =========================================================
            // model-overview
            // =========================================================
            var overview = new StringBuilder();

            var domainLine = BuildDomainLine(headerInfo);
            var modelLine = BuildModelLine(headerInfo);

            if (!string.IsNullOrWhiteSpace(domainLine)) overview.AppendLine(domainLine);
            if (!string.IsNullOrWhiteSpace(modelLine)) overview.AppendLine(modelLine);

            if (!string.IsNullOrWhiteSpace(headerInfo?.DomainKey))
                overview.AppendLine($"Domain Key: {headerInfo.DomainKey}");

            if (!string.IsNullOrWhiteSpace(domainLine) || !string.IsNullOrWhiteSpace(modelLine))
                overview.AppendLine();

            if (!string.IsNullOrWhiteSpace(ModelName))
                overview.AppendLine($"Model: {ModelName}");

            if (!string.IsNullOrWhiteSpace(Namespace))
                overview.AppendLine($"Namespace: {Namespace}");

            if (!string.IsNullOrWhiteSpace(Domain))
                overview.AppendLine($"Domain Classification: {Domain}");

            if (!string.IsNullOrWhiteSpace(ResourceLibrary))
                overview.AppendLine($"Resource Library: {ResourceLibrary}");

            if (!string.IsNullOrWhiteSpace(Title))
                overview.AppendLine($"Title: {Title}");

            if (!string.IsNullOrWhiteSpace(Description))
                overview.AppendLine($"Description: {Description.Trim()}");

            if (!string.IsNullOrWhiteSpace(Help))
            {
                overview.AppendLine("Help:");
                overview.AppendLine(Help.Trim());
            }

            overview.AppendLine();
            overview.AppendLine("Capabilities:");
            overview.AppendLine($"  Cloneable: {Cloneable}");
            overview.AppendLine($"  CanImport: {CanImport}");
            overview.AppendLine($"  CanExport: {CanExport}");

            var urls = new[]
            {
                ListUIUrl, EditUIUrl, CreateUIUrl, HelpUrl,
                InsertUrl, SaveUrl, UpdateUrl,
                FactoryUrl, GetUrl, GetListUrl, DeleteUrl
            }
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct()
            .ToList();

            if (urls.Count > 0)
            {
                overview.AppendLine();
                overview.AppendLine("UI / API Affordances:");

                foreach (var url in urls)
                    overview.AppendLine("- " + url);
            }

            sections.Add(new SummarySection
            {
                SectionKey = "model-overview",
                SectionType = "Overview",
                Flavor = "ModelMetadataDescription",
                Symbol = symbol,
                SymbolType = "Model",
                DomainKey = headerInfo?.DomainKey,
                ModelClassName = headerInfo?.ModelClassName,
                ModelName = headerInfo?.ModelName,
                SectionNormalizedText = overview.ToString().Trim()
            });

            // =========================================================
            // model-fields
            // =========================================================
            var fieldsText = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(domainLine)) fieldsText.AppendLine(domainLine);
            if (!string.IsNullOrWhiteSpace(modelLine)) fieldsText.AppendLine(modelLine);
            if (!string.IsNullOrWhiteSpace(headerInfo?.DomainKey)) fieldsText.AppendLine($"Domain Key: {headerInfo.DomainKey}");

            if (!string.IsNullOrWhiteSpace(domainLine) || !string.IsNullOrWhiteSpace(modelLine))
                fieldsText.AppendLine();

            fieldsText.AppendLine($"Fields for model {symbol}:");

            if (Fields == null || Fields.Count == 0)
            {
                fieldsText.AppendLine("  (no fields defined)");
            }
            else
            {
                foreach (var f in Fields.OrderBy(f => f.PropertyName))
                {
                    if (f == null) continue;

                    fieldsText.AppendLine("- " + f.PropertyName);

                    var fieldKey = ToCamelCase(f.PropertyName);
                    fieldsText.AppendLine("  Field Key: " + fieldKey);

                    if (!string.IsNullOrWhiteSpace(f.Label))
                        fieldsText.AppendLine("  Label: " + f.Label);

                    if (!string.IsNullOrWhiteSpace(f.Help))
                        fieldsText.AppendLine("  Help: " + f.Help.Trim());

                    if (!string.IsNullOrWhiteSpace(f.Watermark))
                        fieldsText.AppendLine("  Watermark: " + f.Watermark);

                    if (!string.IsNullOrWhiteSpace(f.DataType))
                        fieldsText.AppendLine("  DataType: " + f.DataType);

                    if (!string.IsNullOrWhiteSpace(f.FieldType))
                        fieldsText.AppendLine("  FieldType: " + f.FieldType);

                    if (f.IsRequired)
                        fieldsText.AppendLine("  Required: true");
                }
            }

            sections.Add(new SummarySection
            {
                SectionKey = "model-fields",
                SectionType = "Fields",
                Flavor = "ModelMetadataDescription",
                Symbol = symbol,
                SymbolType = "Model",
                DomainKey = headerInfo?.DomainKey,
                ModelClassName = headerInfo?.ModelClassName,
                ModelName = headerInfo?.ModelName,
                SectionNormalizedText = fieldsText.ToString().Trim()
            });

            // =========================================================
            // model-layouts
            // =========================================================
            var layout = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(domainLine)) layout.AppendLine(domainLine);
            if (!string.IsNullOrWhiteSpace(modelLine)) layout.AppendLine(modelLine);
            if (!string.IsNullOrWhiteSpace(headerInfo?.DomainKey)) layout.AppendLine($"Domain Key: {headerInfo.DomainKey}");

            if (!string.IsNullOrWhiteSpace(domainLine) || !string.IsNullOrWhiteSpace(modelLine))
                layout.AppendLine();

            layout.AppendLine($"Layouts for model {symbol}:");

            if (Layouts?.Form?.Col1Fields?.Any() == true)
                layout.AppendLine("Form.Col1: " + string.Join(", ", Layouts.Form.Col1Fields));

            if (Layouts?.Form?.Col2Fields?.Any() == true)
                layout.AppendLine("Form.Col2: " + string.Join(", ", Layouts.Form.Col2Fields));

            if (Layouts?.Form?.BottomFields?.Any() == true)
                layout.AppendLine("Form.Bottom: " + string.Join(", ", Layouts.Form.BottomFields));

            if (Layouts?.Form?.TabFields?.Any() == true)
            {
                foreach (var tab in Layouts.Form.TabFields)
                    layout.AppendLine($"Tab '{tab.Key}': " + string.Join(", ", tab.Value));
            }

            if (Layouts?.Advanced?.Col1Fields?.Any() == true)
                layout.AppendLine("Advanced.Col1: " + string.Join(", ", Layouts.Advanced.Col1Fields));

            if (Layouts?.Advanced?.Col2Fields?.Any() == true)
                layout.AppendLine("Advanced.Col2: " + string.Join(", ", Layouts.Advanced.Col2Fields));

            if (Layouts?.InlineFields?.Any() == true)
                layout.AppendLine("Inline: " + string.Join(", ", Layouts.InlineFields));

            if (Layouts?.MobileFields?.Any() == true)
                layout.AppendLine("Mobile: " + string.Join(", ", Layouts.MobileFields));

            if (Layouts?.SimpleFields?.Any() == true)
                layout.AppendLine("Simple: " + string.Join(", ", Layouts.SimpleFields));

            if (Layouts?.QuickCreateFields?.Any() == true)
                layout.AppendLine("QuickCreate: " + string.Join(", ", Layouts.QuickCreateFields));

            if (Layouts?.AdditionalActions?.Any() == true)
            {
                layout.AppendLine("Additional Actions:");

                foreach (var action in Layouts.AdditionalActions)
                {
                    if (action == null) continue;

                    layout.AppendLine("- " + action.Key + " (" + action.Title + ")");

                    if (!string.IsNullOrWhiteSpace(action.Icon))
                        layout.AppendLine("  Icon: " + action.Icon);

                    if (!string.IsNullOrWhiteSpace(action.Help))
                        layout.AppendLine("  Help: " + action.Help.Trim());

                    layout.AppendLine("  ForCreate: " + action.ForCreate + ", ForEdit: " + action.ForEdit);
                }
            }

            sections.Add(new SummarySection
            {
                SectionKey = "model-layouts",
                SectionType = "Layouts",
                Flavor = "ModelMetadataDescription",
                Symbol = symbol,
                SymbolType = "Model",
                DomainKey = headerInfo?.DomainKey,
                ModelClassName = headerInfo?.ModelClassName,
                ModelName = headerInfo?.ModelName,
                SectionNormalizedText = layout.ToString().Trim()
            });

            return sections;
        }

        // ----------------------------------------
        // Helpers
        // ----------------------------------------

        private static string BuildDomainLine(DomainModelHeaderInformation header)
        {
            if (header == null) return null;

            var hasName = !string.IsNullOrWhiteSpace(header.DomainName);
            var hasTagline = !string.IsNullOrWhiteSpace(header.DomainTagLine);

            if (!hasName && !hasTagline) return null;

            if (hasName && hasTagline)
                return "Domain: " + header.DomainName + " — " + header.DomainTagLine;

            return hasName ? "Domain: " + header.DomainName : header.DomainTagLine;
        }

        private static string BuildModelLine(DomainModelHeaderInformation header)
        {
            if (header == null) return null;

            var modelName = !string.IsNullOrWhiteSpace(header.ModelName)
                ? header.ModelName
                : header.ModelClassName;

            var hasName = !string.IsNullOrWhiteSpace(modelName);
            var hasTagline = !string.IsNullOrWhiteSpace(header.ModelTagLine);

            if (!hasName && !hasTagline) return null;

            if (hasName && hasTagline)
                return "Model: " + modelName + " — " + header.ModelTagLine;

            return hasName ? "Model: " + modelName : header.ModelTagLine;
        }

        private static string ToCamelCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            if (value.Length == 1) return value.ToLowerInvariant();
            return char.ToLowerInvariant(value[0]) + value.Substring(1);
        }
    }
}
