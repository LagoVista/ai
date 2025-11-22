using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// SummarySection implementation for ModelMetadataDescription (IDX-0038).
    ///
    /// NOTE: primary declaration should be:
    ///   public sealed partial class ModelMetadataDescription
    /// </summary>
    public sealed partial class ModelMetadataDescription : ISummarySectionBuilder
    {
        public IEnumerable<SummarySection> BuildSections()
        {
            var symbol = string.IsNullOrWhiteSpace(ModelName) ? "(unknown-model)" : ModelName;
            var sections = new List<SummarySection>();

            var overview = new StringBuilder();
            overview.AppendLine($"Model: {symbol}");

            if (!string.IsNullOrWhiteSpace(Namespace))
                overview.AppendLine($"Namespace: {Namespace}");

            if (!string.IsNullOrWhiteSpace(Domain))
                overview.AppendLine($"Domain: {Domain}");

            if (!string.IsNullOrWhiteSpace(ResourceLibrary))
                overview.AppendLine($"Resource Library: {ResourceLibrary}");

            if (!string.IsNullOrWhiteSpace(Title))
                overview.AppendLine($"Title: {Title}");

            if (!string.IsNullOrWhiteSpace(Description))
                overview.AppendLine($"Description: {Description}");

            if (!string.IsNullOrWhiteSpace(Help))
            {
                overview.AppendLine("Help:");
                overview.AppendLine(Help.Trim());
            }

            overview.AppendLine($"Cloneable: {Cloneable}, Import: {CanImport}, Export: {CanExport}");

            var urls = new[]
            {
                ListUIUrl,
                EditUIUrl,
                CreateUIUrl,
                HelpUrl,
                InsertUrl,
                SaveUrl,
                UpdateUrl,
                FactoryUrl,
                GetUrl,
                GetListUrl,
                DeleteUrl
            }
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct()
            .ToList();

            if (urls.Count > 0)
            {
                overview.AppendLine();
                overview.AppendLine("URLs:");
                foreach (var url in urls)
                    overview.AppendLine($" - {url}");
            }

            sections.Add(new SummarySection
            {
                SectionKey = "model-overview",
                Symbol = symbol,
                SymbolType = "Model",
                SectionNormalizedText = overview.ToString().Trim()
            });

            var fieldsText = new StringBuilder();
            fieldsText.AppendLine($"Fields for model {symbol}:");

            if (Fields == null || Fields.Count == 0)
            {
                fieldsText.AppendLine("No fields defined.");
            }
            else
            {
                foreach (var f in Fields.OrderBy(f => f.PropertyName))
                {
                    fieldsText.AppendLine($"- {f.PropertyName}");
                    if (!string.IsNullOrWhiteSpace(f.Label)) fieldsText.AppendLine($"  Label: {f.Label}");
                    if (!string.IsNullOrWhiteSpace(f.Help)) fieldsText.AppendLine($"  Help: {f.Help}");
                    if (!string.IsNullOrWhiteSpace(f.Watermark)) fieldsText.AppendLine($"  Watermark: {f.Watermark}");
                    if (!string.IsNullOrWhiteSpace(f.DataType)) fieldsText.AppendLine($"  DataType: {f.DataType}");
                    if (!string.IsNullOrWhiteSpace(f.FieldType)) fieldsText.AppendLine($"  FieldType: {f.FieldType}");
                    if (f.IsRequired) fieldsText.AppendLine("  Required: true");
                }
            }

            sections.Add(new SummarySection
            {
                SectionKey = "model-fields",
                Symbol = symbol,
                SymbolType = "Model",
                SectionNormalizedText = fieldsText.ToString().Trim()
            });

            var layout = new StringBuilder();
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
                    layout.AppendLine($"Tab '{tab.Key}': {string.Join(", ", tab.Value)}");
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
                    layout.AppendLine($"- {action.Key} ({action.Title})");
                    if (!string.IsNullOrWhiteSpace(action.Icon)) layout.AppendLine($"  Icon: {action.Icon}");
                    if (!string.IsNullOrWhiteSpace(action.Help)) layout.AppendLine($"  Help: {action.Help}");
                    layout.AppendLine($"  ForCreate: {action.ForCreate}, ForEdit: {action.ForEdit}");
                }
            }

            sections.Add(new SummarySection
            {
                SectionKey = "model-layouts",
                Symbol = symbol,
                SymbolType = "Model",
                SectionNormalizedText = layout.ToString().Trim()
            });

            return sections;
        }
    }
}
