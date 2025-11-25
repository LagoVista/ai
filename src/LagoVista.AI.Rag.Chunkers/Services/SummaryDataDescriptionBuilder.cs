using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// Builds IDX-0052 SummaryDataDescription instances from raw C# source
    /// that contains one or more SummaryData-derived types.
    ///
    /// This builder:
    /// - Uses Roslyn to locate classes that inherit from SummaryData.
    /// - Extracts property information for field/column descriptions.
    /// - Applies simple heuristics to infer the underlying entity name.
    /// - Produces a SummaryDataDescription with human-readable text suitable
    ///   for RAG and documentation.
    ///
    /// It does not perform any chunking; downstream components are
    /// responsible for projecting the description into NormalizedChunks.
    /// </summary>
    public static class SummaryDataDescriptionBuilder
    {
        private static readonly HashSet<string> BaseSummaryDataPropertyNames = new HashSet<string>(
            new[]
            {
                "Id",
                "Icon",
                "IsPublic",
                "Name",
                "Key",
                "Description",
                "IsDeleted",
                "IsDraft",
                "Category",
                "CategoryId",
                "CategoryKey",
                "DiscussionsTotal",
                "DiscussionsOpen",
                "Stars",
                "RatingsCount",
                "LastUpdatedDate"
            },
            StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Entry point used by the indexing pipeline when an IndexFileContext
        /// is available.
        ///
        /// The returned SummaryDataDescription will have common properties
        /// (document identity, paths, etc.) populated from the context via
        /// SetCommonProperties.
        /// </summary>
        public static InvokeResult<SummaryDataDescription> FromSource(
            IndexFileContext ctx,
            string sourceText,
            IReadOnlyDictionary<string, string> resources)
        {
            var description = FromSource(sourceText, resources);
            description.Result.SetCommonProperties(ctx);
            return description;
        }

        /// <summary>
        /// Convenience overload when no resources are provided.
        /// </summary>
        public static InvokeResult<SummaryDataDescription> FromSource(string sourceText)
        {
            return FromSource(sourceText, new Dictionary<string, string>());
        }

        /// <summary>
        /// Core builder that analyzes the source text for a SummaryData-derived
        /// class and constructs a SummaryDataDescription.
        /// </summary>
        public static InvokeResult<SummaryDataDescription> FromSource(
            string sourceText,
            IReadOnlyDictionary<string, string> resources)
        {
            if (sourceText == null) throw new ArgumentNullException(nameof(sourceText));
            if (resources == null) throw new ArgumentNullException(nameof(resources));

            var tree = CSharpSyntaxTree.ParseText(sourceText);
            var root = tree.GetRoot();

            var summaryClass = root
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(IsSummaryDataDerived);

            if (summaryClass == null)
            {
                var missing = InvokeResult<SummaryDataDescription>.Create(null);
                missing.AddUserError("No SummaryData-derived class was found in the provided source.");
                return missing;
            }

            var @namespace = GetNamespace(summaryClass);
            var summaryTypeName = summaryClass.Identifier.Text;
            var qualifiedName = string.IsNullOrWhiteSpace(@namespace)
                ? summaryTypeName
                : @namespace + "." + summaryTypeName;

            var fields = BuildFieldDescriptions(summaryClass).ToList();
            var underlyingEntity = InferUnderlyingEntityName(summaryTypeName);

            var listName = !string.IsNullOrWhiteSpace(underlyingEntity)
                ? $"{underlyingEntity} List"
                : summaryTypeName;

            var descriptionText = BuildDescriptionText(listName, underlyingEntity, fields);
            var behaviorText = BuildBehaviorText(fields);

            var description = new SummaryDataDescription
            {
                // Identity
                ListName = listName,
                SummaryTypeName = qualifiedName,
                UnderlyingEntityTypeName = underlyingEntity,
                Domain = null, // Domain can be populated later from higher-level metadata.
                QualifiedName = qualifiedName,

                // Human text
                Title = listName,
                Description = descriptionText,
                Help = null,
                BehaviorDescription = behaviorText,

                // Navigation – these can be enriched later from entity descriptions
                ListUIUrl = null,
                GetListUrl = null,

                // Fields
                Fields = fields
            };

            return InvokeResult<SummaryDataDescription>.Create(description);
        }

        // -------------------- helpers --------------------

        private static bool IsSummaryDataDerived(ClassDeclarationSyntax classDecl)
        {
            if (classDecl.BaseList == null) return false;

            foreach (var bt in classDecl.BaseList.Types)
            {
                var baseTypeText = bt.Type.ToString();
                var simple = GetSimpleTypeName(baseTypeText);
                if (string.Equals(simple, "SummaryData", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetNamespace(SyntaxNode node)
        {
            var current = node;
            while (current != null)
            {
                if (current is NamespaceDeclarationSyntax ns)
                {
                    return ns.Name.ToString();
                }

                if (current is FileScopedNamespaceDeclarationSyntax fns)
                {
                    return fns.Name.ToString();
                }

                current = current.Parent;
            }

            return null;
        }

        private static IEnumerable<SummaryDataFieldDescription> BuildFieldDescriptions(ClassDeclarationSyntax summaryClass)
        {
            foreach (var member in summaryClass.Members.OfType<PropertyDeclarationSyntax>())
            {
                var name = member.Identifier.Text;
                var clrType = member.Type.ToString();

                var (isVisible, header) = GetListColumnMetadata(member);

                yield return new SummaryDataFieldDescription
                {
                    Name = name,
                    ClrType = clrType,
                    IsVisible = isVisible,
                    Header = header,
                    IsBaseSummaryDataField = BaseSummaryDataPropertyNames.Contains(name)
                };
            }
        }

        private static (bool isVisible, string header) GetListColumnMetadata(PropertyDeclarationSyntax prop)
        {
            bool isVisible = true;
            string header = null;

            foreach (var attrList in prop.AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var rawName = attr.Name.ToString();
                    var simple = GetSimpleTypeName(rawName);

                    // Normalize Attribute suffix
                    if (simple.EndsWith("Attribute", StringComparison.Ordinal))
                    {
                        simple = simple.Substring(0, simple.Length - "Attribute".Length);
                    }

                    if (!string.Equals(simple, "ListColumn", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Process arguments…
                    if (attr.ArgumentList != null)
                    {
                        foreach (var arg in attr.ArgumentList.Arguments)
                        {
                            if (arg.NameEquals != null &&
                                string.Equals(arg.NameEquals.Name.Identifier.Text, "Visible", StringComparison.Ordinal))
                            {
                                if (arg.Expression is LiteralExpressionSyntax lit &&
                                    lit.IsKind(SyntaxKind.FalseLiteralExpression))
                                {
                                    isVisible = false;
                                }
                            }

                            if (arg.NameEquals != null &&
                                string.Equals(arg.NameEquals.Name.Identifier.Text, "Header", StringComparison.Ordinal))
                            {
                                if (arg.Expression is LiteralExpressionSyntax headerLit &&
                                    headerLit.IsKind(SyntaxKind.StringLiteralExpression))
                                {
                                    header = headerLit.Token.ValueText;
                                }
                            }

                            if (arg.NameColon != null &&
                                string.Equals(arg.NameColon.Name.Identifier.Text, "Visible", StringComparison.Ordinal))
                            {
                                if (arg.Expression is LiteralExpressionSyntax lit &&
                                    lit.IsKind(SyntaxKind.FalseLiteralExpression))
                                {
                                    isVisible = false;
                                }
                            }

                            if (arg.NameColon != null &&
                                string.Equals(arg.NameColon.Name.Identifier.Text, "Header", StringComparison.Ordinal))
                            {
                                if (arg.Expression is LiteralExpressionSyntax headerLit &&
                                    headerLit.IsKind(SyntaxKind.StringLiteralExpression))
                                {
                                    header = headerLit.Token.ValueText;
                                }
                            }

                        }
                    }
                }
            }

            return (isVisible, header);
        }


        /// <summary>
        /// Attempt to infer the underlying entity name from a SummaryData type
        /// name. For example, "DeviceSummary" -&gt; "Device".
        /// Returns null when a simple heuristic cannot determine a name.
        /// </summary>
        private static string InferUnderlyingEntityName(string summaryTypeName)
        {
            if (string.IsNullOrWhiteSpace(summaryTypeName)) return null;

            const string suffix = "Summary";
            if (summaryTypeName.EndsWith(suffix, StringComparison.Ordinal) && summaryTypeName.Length > suffix.Length)
            {
                return summaryTypeName.Substring(0, summaryTypeName.Length - suffix.Length);
            }

            return null;
        }

        /// <summary>
        /// Build a high-level description sentence or paragraph that explains
        /// what the list shows and how it is typically used.
        /// </summary>
        private static string BuildDescriptionText(
            string listName,
            string underlyingEntity,
            IReadOnlyCollection<SummaryDataFieldDescription> fields)
        {
            var entityName = underlyingEntity ?? "item";

            var hasName = fields.Any(f => string.Equals(f.Name, "Name", StringComparison.OrdinalIgnoreCase));
            var hasKey = fields.Any(f => string.Equals(f.Name, "Key", StringComparison.OrdinalIgnoreCase));
            var hasCategory = fields.Any(f => string.Equals(f.Name, "Category", StringComparison.OrdinalIgnoreCase));
            var hasIsDeleted = fields.Any(f => string.Equals(f.Name, "IsDeleted", StringComparison.OrdinalIgnoreCase));
            var hasIsDraft = fields.Any(f => string.Equals(f.Name, "IsDraft", StringComparison.OrdinalIgnoreCase));

            var description = $"{listName} shows a list of {entityName} summaries derived from the core SummaryData shape.";

            if (hasName || hasKey || hasCategory)
            {
                description += " Each row exposes identifying fields such as";
                var parts = new List<string>();
                if (hasName) parts.Add("Name");
                if (hasKey) parts.Add("Key");
                if (hasCategory) parts.Add("Category");
                description += " " + string.Join(", ", parts) + ".";
            }

            if (hasIsDeleted || hasIsDraft)
            {
                description += " Lifecycle fields indicate soft-deleted and draft items when present.";
            }

            return description;
        }

        /// <summary>
        /// Build a behavior-focused narrative that an LLM can use to reason
        /// about how the list behaves (soft delete, draft/published,
        /// ratings, discussions, etc.).
        /// </summary>
        private static string BuildBehaviorText(IReadOnlyCollection<SummaryDataFieldDescription> fields)
        {
            var hasIsDeleted = fields.Any(f => string.Equals(f.Name, "IsDeleted", StringComparison.OrdinalIgnoreCase));
            var hasIsDraft = fields.Any(f => string.Equals(f.Name, "IsDraft", StringComparison.OrdinalIgnoreCase));
            var hasStars = fields.Any(f => string.Equals(f.Name, "Stars", StringComparison.OrdinalIgnoreCase));
            var hasRatings = fields.Any(f => string.Equals(f.Name, "RatingsCount", StringComparison.OrdinalIgnoreCase));
            var hasDiscussions = fields.Any(f => string.Equals(f.Name, "DiscussionsTotal", StringComparison.OrdinalIgnoreCase));
            var hasUpdated = fields.Any(f => string.Equals(f.Name, "LastUpdatedDate", StringComparison.OrdinalIgnoreCase));

            var segments = new List<string>();

            if (hasIsDeleted)
            {
                segments.Add("Soft-delete is represented by the IsDeleted flag; deleted items may be hidden or shown via filters.");
            }

            if (hasIsDraft)
            {
                segments.Add("Draft versus published state is represented by the IsDraft flag, allowing lists to show or hide drafts.");
            }

            if (hasStars || hasRatings)
            {
                segments.Add("Rating information is conveyed through Stars and RatingsCount when present, providing quality or popularity signals.");
            }

            if (hasDiscussions)
            {
                segments.Add("Discussion activity can be surfaced via DiscussionsTotal and DiscussionsOpen-style fields for collaboration counts.");
            }

            if (hasUpdated)
            {
                segments.Add("Recency is indicated by LastUpdatedDate, which can be used for sorting or understanding freshness.");
            }

            if (segments.Count == 0)
            {
                return "This list exposes one row per entity, providing a lightweight summary surface built on the core SummaryData fields.";
            }

            return string.Join(" ", segments);
        }

        private static string GetSimpleTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return typeName;

            var simple = typeName;

            var lt = simple.IndexOf('<');
            if (lt >= 0)
                simple = simple.Substring(0, lt);

            var dot = simple.LastIndexOf('.');
            if (dot >= 0 && dot < simple.Length - 1)
                simple = simple.Substring(dot + 1);

            return simple;
        }
    }
}
