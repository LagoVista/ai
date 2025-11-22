using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// SummarySection implementation for ModelStructureDescription (IDX-0037).
    ///
    /// NOTE: primary declaration should be:
    ///   public sealed partial class ModelStructureDescription
    /// </summary>
    public sealed partial class ModelStructureDescription : ISummarySectionBuilder
    {
        public IEnumerable<SummarySection> BuildSections(DomainModelHeaderInformation headerInfo, int maxTokens = 6500)
        {
            var symbol = string.IsNullOrWhiteSpace(ModelName) ? "(unknown-model)" : ModelName;
            var sections = new List<SummarySection>();

            var overview = new StringBuilder();
            overview.AppendLine($"Model: {symbol}");

            if (!string.IsNullOrWhiteSpace(Namespace))
                overview.AppendLine($"Namespace: {Namespace}");

            if (!string.IsNullOrWhiteSpace(QualifiedName))
                overview.AppendLine($"Qualified Name: {QualifiedName}");

            if (!string.IsNullOrWhiteSpace(Domain))
                overview.AppendLine($"Domain: {Domain}");

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
                SectionKey = "model-structure-overview",
                Symbol = symbol,
                SymbolType = "Model",
                SectionNormalizedText = overview.ToString().Trim()
            });

            var props = new StringBuilder();
            props.AppendLine($"Properties for model {symbol}:");

            if (Properties == null || Properties.Count == 0)
            {
                props.AppendLine("No properties defined.");
            }
            else
            {
                foreach (var p in Properties.OrderBy(p => p.Name))
                {
                    props.AppendLine($"- {p.Name} : {p.ClrType}");
                    if (p.IsCollection) props.AppendLine("  Collection: true");
                    if (p.IsValueType) props.AppendLine("  ValueType: true");
                    if (p.IsEnum) props.AppendLine("  Enum: true");
                    if (p.IsKey) props.AppendLine("  Key: true");
                    if (!string.IsNullOrWhiteSpace(p.Group)) props.AppendLine($"  Group: {p.Group}");
                    if (!string.IsNullOrWhiteSpace(p.EntityHeaderRefKey)) props.AppendLine($"  EntityHeaderRef: {p.EntityHeaderRefKey}");
                    if (!string.IsNullOrWhiteSpace(p.ChildObjectKey)) props.AppendLine($"  ChildObject: {p.ChildObjectKey}");
                }
            }

            sections.Add(new SummarySection
            {
                SectionKey = "model-structure-properties",
                Symbol = symbol,
                SymbolType = "Model",
                SectionNormalizedText = props.ToString().Trim()
            });

            var relationships = new StringBuilder();
            relationships.AppendLine($"Relationships for model {symbol}:");

            var hasContent = false;

            if (EntityHeaderRefs != null && EntityHeaderRefs.Count > 0)
            {
                hasContent = true;
                relationships.AppendLine("EntityHeader references:");
                foreach (var eh in EntityHeaderRefs)
                {
                    relationships.AppendLine($"- {eh.Key}: {eh.PropertyName} -> {eh.TargetType} (Domain={eh.Domain}, Collection={eh.IsCollection})");
                }
            }

            if (ChildObjects != null && ChildObjects.Count > 0)
            {
                hasContent = true;
                relationships.AppendLine();
                relationships.AppendLine("Child objects:");
                foreach (var child in ChildObjects)
                {
                    relationships.AppendLine($"- {child.Key}: {child.PropertyName} : {child.ClrType} (Collection={child.IsCollection})");
                    if (!string.IsNullOrWhiteSpace(child.Title)) relationships.AppendLine($"  Title: {child.Title}");
                    if (!string.IsNullOrWhiteSpace(child.Description)) relationships.AppendLine($"  Description: {child.Description}");
                }
            }

            if (Relationships != null && Relationships.Count > 0)
            {
                hasContent = true;
                relationships.AppendLine();
                relationships.AppendLine("Explicit relationships:");
                foreach (var rel in Relationships)
                {
                    relationships.AppendLine($"- {rel.Name}: {rel.FromModel} -> {rel.ToModel} ({rel.Cardinality})");
                    if (!string.IsNullOrWhiteSpace(rel.SourceProperty)) relationships.AppendLine($"  SourceProperty: {rel.SourceProperty}");
                    if (!string.IsNullOrWhiteSpace(rel.TargetProperty)) relationships.AppendLine($"  TargetProperty: {rel.TargetProperty}");
                    if (!string.IsNullOrWhiteSpace(rel.Description)) relationships.AppendLine($"  Description: {rel.Description}");
                }
            }

            if (!hasContent)
            {
                relationships.AppendLine("No relationships modeled.");
            }

            sections.Add(new SummarySection
            {
                SectionKey = "model-structure-relationships",
                Symbol = symbol,
                SymbolType = "Model",
                SectionNormalizedText = relationships.ToString().Trim()
            });

            return sections;
        }
    }
}
