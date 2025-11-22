using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LagoVista.AI.Rag.Chunkers.Services;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// SummarySection implementation for ModelStructureDescription (IDX-0037).
    ///
    /// Converts structured model metadata (properties, relationships, affordances)
    /// into human-readable sections suitable for embedding, while propagating
    /// domain/model taglines per the structured summary DDRs.
    /// </summary>
    public sealed partial class ModelStructureDescription : ISummarySectionBuilder
    {
        /// <summary>
        /// Builds human-readable summary sections for this model structure,
        /// enriched with domain/model context.
        ///
        /// For V1 we emit three sections:
        /// - model-structure-overview
        /// - model-structure-properties
        /// - model-structure-relationships
        ///
        /// Sections are kept as single blocks; maxTokens is accepted for
        /// contract consistency but not used for intra-section splitting yet.
        /// </summary>
        public IEnumerable<SummarySection> BuildSections(
            DomainModelHeaderInformation headerInfo,
            int maxTokens = 6500)
        {
            if (maxTokens <= 0)
            {
                maxTokens = 6500;
            }

            var sections = new List<SummarySection>();

            // Symbol: prefer logical model name; fall back to CLR model name.
            var symbol = !string.IsNullOrWhiteSpace(headerInfo?.ModelName)
                ? headerInfo.ModelName
                : !string.IsNullOrWhiteSpace(ModelName)
                    ? ModelName
                    : "(unknown-model)";

            // =====================================================================
            // model-structure-overview
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

            // Explicit DomainKey line from header info when available.
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

            // High-level identity / semantics
            if (!string.IsNullOrWhiteSpace(ModelName))
            {
                overview.AppendLine($"Model: {ModelName}");
            }

            if (!string.IsNullOrWhiteSpace(Namespace))
            {
                overview.AppendLine($"Namespace: {Namespace}");
            }

            if (!string.IsNullOrWhiteSpace(QualifiedName))
            {
                overview.AppendLine($"Qualified Name: {QualifiedName}");
            }

            // Domain classification from the model itself (may differ from DomainKey).
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

            // Capabilities
            overview.AppendLine();
            overview.AppendLine("Capabilities:");
            overview.AppendLine($"  Cloneable: {Cloneable}");
            overview.AppendLine($"  CanImport: {CanImport}");
            overview.AppendLine($"  CanExport: {CanExport}");

            // UI / API affordances including URLs (List/Detail/factory and API endpoints).
            var affordanceLines = new List<string>();

            if (!string.IsNullOrWhiteSpace(ListUIUrl)) affordanceLines.Add($"List UI: {ListUIUrl}");
            if (!string.IsNullOrWhiteSpace(EditUIUrl)) affordanceLines.Add($"Edit UI: {EditUIUrl}");
            if (!string.IsNullOrWhiteSpace(CreateUIUrl)) affordanceLines.Add($"Create UI: {CreateUIUrl}");
            if (!string.IsNullOrWhiteSpace(HelpUrl)) affordanceLines.Add($"Help: {HelpUrl}");

            if (!string.IsNullOrWhiteSpace(InsertUrl)) affordanceLines.Add($"Insert: {InsertUrl}");
            if (!string.IsNullOrWhiteSpace(SaveUrl)) affordanceLines.Add($"Save: {SaveUrl}");
            if (!string.IsNullOrWhiteSpace(UpdateUrl)) affordanceLines.Add($"Update: {UpdateUrl}");
            if (!string.IsNullOrWhiteSpace(FactoryUrl)) affordanceLines.Add($"Factory: {FactoryUrl}");
            if (!string.IsNullOrWhiteSpace(GetUrl)) affordanceLines.Add($"Get: {GetUrl}");
            if (!string.IsNullOrWhiteSpace(GetListUrl)) affordanceLines.Add($"Get List: {GetListUrl}");
            if (!string.IsNullOrWhiteSpace(DeleteUrl)) affordanceLines.Add($"Delete: {DeleteUrl}");

            if (affordanceLines.Count > 0)
            {
                overview.AppendLine();
                overview.AppendLine("UI / API Affordances:");
                foreach (var line in affordanceLines)
                {
                    overview.AppendLine("- " + line);
                }
            }

            sections.Add(new SummarySection
            {
                SectionKey = "model-structure-overview",
                SectionType = "Overview",
                Flavor = "ModelStructureDescription",
                Symbol = symbol,
                SymbolType = "Model",
                DomainKey = headerInfo?.DomainKey,
                ModelClassName = headerInfo?.ModelClassName,
                ModelName = headerInfo?.ModelName,
                SectionNormalizedText = overview.ToString().Trim()
            });

            // =====================================================================
            // model-structure-properties
            // =====================================================================
            var propsSection = new StringBuilder();

            domainLine = BuildDomainLine(headerInfo);
            modelLine = BuildModelLine(headerInfo);

            if (!string.IsNullOrWhiteSpace(domainLine))
            {
                propsSection.AppendLine(domainLine);
            }

            if (!string.IsNullOrWhiteSpace(modelLine))
            {
                propsSection.AppendLine(modelLine);
            }

            if (!string.IsNullOrWhiteSpace(headerInfo?.DomainKey))
            {
                propsSection.AppendLine($"Domain Key: {headerInfo.DomainKey}");
            }

            if (!string.IsNullOrWhiteSpace(domainLine) ||
                !string.IsNullOrWhiteSpace(modelLine) ||
                !string.IsNullOrWhiteSpace(headerInfo?.DomainKey))
            {
                propsSection.AppendLine();
            }

            propsSection.AppendLine($"Properties for model {ModelName ?? symbol}:");

            var props = Properties ?? new List<ModelPropertyDescription>();

            if (props.Count == 0)
            {
                propsSection.AppendLine("  (no properties detected)");
            }
            else
            {
                foreach (var prop in props)
                {
                    if (prop == null)
                    {
                        continue;
                    }

                    // Line 1: Name : Type
                    propsSection.AppendLine($"- {prop.Name} : {SummarizeClrType(prop)}");

                    // Line 2: structural flags
                    var flags = new List<string>();

                    if (prop.IsKey) flags.Add("Key: true");
                    if (prop.IsCollection) flags.Add("Collection: true");
                    if (prop.IsValueType) flags.Add("ValueType: true");
                    if (prop.IsEnum) flags.Add("Enum: true");
                    if (!string.IsNullOrWhiteSpace(prop.Group)) flags.Add("Group: " + prop.Group);

                    if (!string.IsNullOrWhiteSpace(prop.EntityHeaderRefKey))
                    {
                        flags.Add("EntityHeaderRefKey: " + prop.EntityHeaderRefKey);
                    }

                    if (!string.IsNullOrWhiteSpace(prop.ChildObjectKey))
                    {
                        flags.Add("ChildObjectKey: " + prop.ChildObjectKey);
                    }

                    if (flags.Count > 0)
                    {
                        propsSection.AppendLine("  " + string.Join(", ", flags));
                    }
                }
            }

            sections.Add(new SummarySection
            {
                SectionKey = "model-structure-properties",
                SectionType = "Properties",
                Flavor = "ModelStructureDescription",
                Symbol = symbol,
                SymbolType = "Model",
                DomainKey = headerInfo?.DomainKey,
                ModelClassName = headerInfo?.ModelClassName,
                ModelName = headerInfo?.ModelName,
                SectionNormalizedText = propsSection.ToString().Trim()
            });

            // =====================================================================
            // model-structure-relationships
            // =====================================================================
            var relSection = new StringBuilder();

            domainLine = BuildDomainLine(headerInfo);
            modelLine = BuildModelLine(headerInfo);

            if (!string.IsNullOrWhiteSpace(domainLine))
            {
                relSection.AppendLine(domainLine);
            }

            if (!string.IsNullOrWhiteSpace(modelLine))
            {
                relSection.AppendLine(modelLine);
            }

            if (!string.IsNullOrWhiteSpace(headerInfo?.DomainKey))
            {
                relSection.AppendLine($"Domain Key: {headerInfo.DomainKey}");
            }

            if (!string.IsNullOrWhiteSpace(domainLine) ||
                !string.IsNullOrWhiteSpace(modelLine) ||
                !string.IsNullOrWhiteSpace(headerInfo?.DomainKey))
            {
                relSection.AppendLine();
            }

            relSection.AppendLine($"Relationships for model {ModelName ?? symbol}:");

            // EntityHeader references
            relSection.AppendLine();
            relSection.AppendLine("EntityHeader references:");

            var headerRefs = EntityHeaderRefs ?? new List<ModelEntityHeaderRefDescription>();
            if (headerRefs.Count == 0)
            {
                relSection.AppendLine("  (none)");
            }
            else
            {
                foreach (var hdr in headerRefs)
                {
                    if (hdr == null)
                    {
                        continue;
                    }

                    relSection.Append("- ");
                    relSection.Append(hdr.PropertyName);
                    relSection.Append(" -> ");
                    relSection.Append(hdr.TargetType);
                    relSection.Append(" (Domain=");
                    relSection.Append(hdr.Domain ?? string.Empty);
                    relSection.Append(", Collection=");
                    relSection.Append(hdr.IsCollection);
                    relSection.AppendLine(")");
                }
            }

            // Child objects
            relSection.AppendLine();
            relSection.AppendLine("Child objects:");

            var children = ChildObjects ?? new List<ModelChildObjectDescription>();
            if (children.Count == 0)
            {
                relSection.AppendLine("  (none)");
            }
            else
            {
                foreach (var child in children)
                {
                    if (child == null)
                    {
                        continue;
                    }

                    relSection.Append("- ");
                    relSection.Append(child.PropertyName);
                    relSection.Append(" : ");
                    relSection.Append(child.ClrType);
                    relSection.Append(" (Collection=");
                    relSection.Append(child.IsCollection);
                    relSection.AppendLine(")");

                    if (!string.IsNullOrWhiteSpace(child.Title))
                    {
                        relSection.AppendLine("  Title: " + child.Title);
                    }

                    if (!string.IsNullOrWhiteSpace(child.Description))
                    {
                        relSection.AppendLine("  " + child.Description.Trim());
                    }
                }
            }

            // Explicit relationships
            relSection.AppendLine();
            relSection.AppendLine("Explicit relationships:");

            var relationships = Relationships ?? new List<ModelRelationshipDescription>();
            if (relationships.Count == 0)
            {
                relSection.AppendLine("  (none)");
            }
            else
            {
                foreach (var rel in relationships)
                {
                    if (rel == null)
                    {
                        continue;
                    }

                    var name = !string.IsNullOrWhiteSpace(rel.Name) ? rel.Name : "(unnamed)";

                    relSection.Append("- ");
                    relSection.Append(name);
                    relSection.Append(": ");
                    relSection.Append(rel.FromModel);
                    relSection.Append(" -> ");
                    relSection.Append(rel.ToModel);

                    if (!string.IsNullOrWhiteSpace(rel.Cardinality))
                    {
                        relSection.Append(" (");
                        relSection.Append(rel.Cardinality);
                        relSection.Append(")");
                    }

                    relSection.AppendLine();

                    if (!string.IsNullOrWhiteSpace(rel.SourceProperty))
                    {
                        relSection.AppendLine("  SourceProperty: " + rel.SourceProperty);
                    }

                    if (!string.IsNullOrWhiteSpace(rel.TargetProperty))
                    {
                        relSection.AppendLine("  TargetProperty: " + rel.TargetProperty);
                    }

                    if (!string.IsNullOrWhiteSpace(rel.Description))
                    {
                        relSection.AppendLine("  " + rel.Description.Trim());
                    }
                }
            }

            sections.Add(new SummarySection
            {
                SectionKey = "model-structure-relationships",
                SectionType = "Relationships",
                Flavor = "ModelStructureDescription",
                Symbol = symbol,
                SymbolType = "Model",
                DomainKey = headerInfo?.DomainKey,
                ModelClassName = headerInfo?.ModelClassName,
                ModelName = headerInfo?.ModelName,
                SectionNormalizedText = relSection.ToString().Trim()
            });

            return sections;
        }

        // ---------------------------------------------------------------------
        // Domain/model header helpers (aligned with Manager/Endpoint flavors)
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

        private static string SummarizeClrType(ModelPropertyDescription prop)
        {
            if (prop == null || string.IsNullOrWhiteSpace(prop.ClrType))
            {
                return "(unknown-type)";
            }

            // For now we just return the CLR type string; later we can
            // normalize common patterns (List<T>, EntityHeader<T>, etc.).
            return prop.ClrType;
        }
    }
}
