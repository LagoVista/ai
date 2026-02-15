using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Chunkers.Providers.ModelMetaData
{
    /// <summary>
    /// Helper for turning IDX-0038 <see cref="ModelMetadataDescription"/> into
    /// a rich, natural-language summary suitable for use in SubKindDetectionResult.SummaryInstructions
    /// and SummarySection content.
    ///
    /// This is intentionally deterministic and template-driven so that the
    /// indexer can rely on consistent wording while still giving the LLM
    /// plenty of semantic hints about fields, capabilities, and layouts.
    /// </summary>
    public static class ModelMetadataSummaryBuilder
    {
        /// <summary>
        /// Build a human-readable summary for a model using its metadata
        /// (labels, help text, capabilities, and layouts).
        ///
        /// Returns null or empty if the input is null or effectively empty.
        /// </summary>
        public static string BuildSummary(ModelMetadataDescription metadata)
        {
            if (metadata == null)
            {
                return string.Empty;
            }

            var name = !string.IsNullOrWhiteSpace(metadata.Title)
                ? metadata.Title
                : metadata.ModelName;

            if (string.IsNullOrWhiteSpace(name) &&
                string.IsNullOrWhiteSpace(metadata.Description) &&
                (metadata.Fields == null || metadata.Fields.Count == 0))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();

            // --- High-level identity ---
            if (!string.IsNullOrWhiteSpace(name))
            {
                if (!string.IsNullOrWhiteSpace(metadata.Domain))
                {
                    sb.AppendLine($"The {name} model belongs to the {metadata.Domain} domain.");
                }
                else
                {
                    sb.AppendLine($"The {name} model represents an entity in the system.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(metadata.ModelName))
            {
                if (!string.IsNullOrWhiteSpace(metadata.Domain))
                {
                    sb.AppendLine($"The {metadata.ModelName} model belongs to the {metadata.Domain} domain.");
                }
                else
                {
                    sb.AppendLine($"The {metadata.ModelName} model represents an entity in the system.");
                }
            }

            if (!string.IsNullOrWhiteSpace(metadata.Description))
            {
                sb.AppendLine(metadata.Description.Trim());
            }
            else if (!string.IsNullOrWhiteSpace(metadata.Help))
            {
                sb.AppendLine(metadata.Help.Trim());
            }

            // --- Capabilities ---
            var capabilities = new List<string>();
            if (metadata.Cloneable) capabilities.Add("can be cloned to speed setup");
            if (metadata.CanImport) capabilities.Add("supports bulk import");
            if (metadata.CanExport) capabilities.Add("supports bulk export");

            if (capabilities.Count > 0)
            {
                sb.Append("It ");
                sb.Append(string.Join(", ", capabilities));
                sb.AppendLine(".");
            }

            // --- Key fields ---
            var fields = metadata.Fields ?? new List<ModelFieldMetadataDescription>();
            if (fields.Count > 0)
            {
                var primaryFieldNames = CollectPrimaryFieldNames(metadata.Layouts, fields);

                var primaryFields = fields
                    .Where(f => !string.IsNullOrWhiteSpace(f.PropertyName))
                    .Where(f => primaryFieldNames.Contains(f.PropertyName, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (primaryFields.Count == 0)
                {
                    // Fallback: take first few fields if layouts are empty or misaligned
                    primaryFields = fields.Take(6).ToList();
                }

                sb.AppendLine();
                sb.AppendLine("Key fields:");

                foreach (var field in primaryFields)
                {
                    var displayName = !string.IsNullOrWhiteSpace(field.Label)
                        ? field.Label
                        : field.PropertyName;

                    var typeParts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(field.DataType))
                    {
                        typeParts.Add(field.DataType);
                    }
                    if (!string.IsNullOrWhiteSpace(field.FieldType) &&
                        !string.Equals(field.FieldType, field.DataType, StringComparison.OrdinalIgnoreCase))
                    {
                        typeParts.Add(field.FieldType);
                    }

                    var typeDescription = typeParts.Count > 0
                        ? string.Join(", ", typeParts)
                        : null;

                    var requirement = field.IsRequired ? "required" : "optional";

                    sb.Append("- ");
                    sb.Append(displayName);

                    sb.Append(" (");
                    sb.Append(field.PropertyName);
                    if (!string.IsNullOrWhiteSpace(typeDescription))
                    {
                        sb.Append(", ");
                        sb.Append(typeDescription);
                    }
                    sb.Append(", ");
                    sb.Append(requirement);
                    sb.Append(")");

                    var help = field.Help;
                    if (string.IsNullOrWhiteSpace(help) && !string.IsNullOrWhiteSpace(field.Watermark))
                    {
                        help = field.Watermark;
                    }

                    if (!string.IsNullOrWhiteSpace(help))
                    {
                        sb.Append(" – ");
                        sb.Append(help.Trim());
                    }

                    sb.AppendLine();
                }
            }

            return sb.ToString().Trim();
        }

        private static HashSet<string> CollectPrimaryFieldNames(ModelFormLayouts layouts, List<ModelFieldMetadataDescription> allFields)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (layouts == null)
            {
                return result;
            }

            void AddList(IEnumerable<string> names)
            {
                if (names == null) return;
                foreach (var name in names)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        result.Add(name);
                    }
                }
            }

            if (layouts.Form != null)
            {
                AddList(layouts.Form.Col1Fields);
                AddList(layouts.Form.Col2Fields);
                AddList(layouts.Form.BottomFields);

                if (layouts.Form.TabFields != null)
                {
                    foreach (var kvp in layouts.Form.TabFields)
                    {
                        AddList(kvp.Value);
                    }
                }
            }

            if (layouts.QuickCreateFields != null && layouts.QuickCreateFields.Count > 0)
            {
                AddList(layouts.QuickCreateFields);
            }

            if (layouts.SimpleFields != null && layouts.SimpleFields.Count > 0)
            {
                AddList(layouts.SimpleFields);
            }

            // If we collected nothing but have fields, fall back later.
            return result;
        }
    }
}
