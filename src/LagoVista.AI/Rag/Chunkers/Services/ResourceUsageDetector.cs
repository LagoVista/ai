using System;
using System.Collections.Generic;
using System.Linq;
using LagoVista.AI.Rag.Chunkers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// IDX-0052 – Resource usage detector (Roslyn + source text).
    ///
    /// Public API takes raw C# source text and basic identity info.
    /// Internally we use Roslyn to build a syntax tree so we can
    /// understand attributes and enclosing types, but callers only
    /// need to provide the file text.
    /// </summary>
    public static class ResourceUsageDetector
    {
        /// <summary>
        /// Detects resource usages in a single C# source file.
        /// This method parses the source with Roslyn, but callers
        /// only provide raw text and basic identity information.
        /// </summary>
        public static IReadOnlyList<ResourceUsageRecord> DetectUsages(
            string sourceText,
            string orgId,
            string projectId,
            string repoId,
            string relativePath)
        {
            if (string.IsNullOrWhiteSpace(sourceText)) throw new ArgumentNullException(nameof(sourceText));
            if (orgId == null) throw new ArgumentNullException(nameof(orgId));
            if (projectId == null) throw new ArgumentNullException(nameof(projectId));
            if (repoId == null) throw new ArgumentNullException(nameof(repoId));
            if (relativePath == null) throw new ArgumentNullException(nameof(relativePath));

            var records = new List<ResourceUsageRecord>();

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
            var root = syntaxTree.GetRoot();

            var normalizedPath = NormalizePath(relativePath);
            var isTest = IsTestPath(normalizedPath);

            // 1) Attribute-driven usages (high-signal metadata).
            DetectAttributeBasedUsages(root, records, orgId, projectId, repoId, normalizedPath, isTest);

            // 2) General member-access usages (fallback / non-metadata).
            DetectMemberAccessUsages(root, records, orgId, projectId, repoId, normalizedPath, isTest);

            return records;
        }

        #region Attribute-driven detection

        private static readonly Dictionary<(string AttributeName, string PropertyName), ResourceUsageKind> AttributeUsageMap =
            new Dictionary<(string AttributeName, string PropertyName), ResourceUsageKind>(new AttributePairComparer())
            {
                // FormFieldAttribute (named)
            { ("FormField", "LabelResource"),      ResourceUsageKind.FormFieldLabel},
            { ("FormField", "HelpResource"),       ResourceUsageKind.FormFieldHelp },
            { ("FormField", "WaterMark"),  ResourceUsageKind.Watermark },
            { ("FormField", "ColHeaderResource"),  ResourceUsageKind.ColumnHeader },
            { ("FormField", "ReqMessageResource"), ResourceUsageKind.ValidationMessage },
            { ("FormField", "RegExValidationMessageResource"), ResourceUsageKind.ValidationMessage },

                // EntityDescriptionAttribute (named form; positional handled separately)
                //{ ("EntityDescription", "TitleResource"),        ResourceUsageKind.ModelTitle },
                //{ ("EntityDescription", "UserHelpResource"),  ResourceUsageKind.ModelHelp },
                ///{ ("EntityDescription", "DescriptionResource"), ResourceUsageKind.ModelDescription },

                // EnumLabel (named HelpResource)
                { ("EnumLabel", "HelpResource"), ResourceUsageKind.EnumHelp },
            };

        /// <summary>
        /// Case-insensitive comparer for (AttributeName, PropertyName) keys.
        /// </summary>
        private sealed class AttributePairComparer
            : IEqualityComparer<(string AttributeName, string PropertyName)>
        {
            public bool Equals(
                (string AttributeName, string PropertyName) x,
                (string AttributeName, string PropertyName) y)
            {
                return string.Equals(x.AttributeName, y.AttributeName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.PropertyName, y.PropertyName, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode((string AttributeName, string PropertyName) obj)
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + (obj.AttributeName?.ToLowerInvariant().GetHashCode() ?? 0);
                    hash = hash * 23 + (obj.PropertyName?.ToLowerInvariant().GetHashCode() ?? 0);
                    return hash;
                }
            }
        }

        private static void DetectAttributeBasedUsages(
            SyntaxNode root,
            List<ResourceUsageRecord> records,
            string orgId,
            string projectId,
            string repoId,
            string relativePath,
            bool isTest)
        {
            var attributes = root.DescendantNodes()
                .OfType<AttributeSyntax>();

            foreach (var attribute in attributes)
            {
                var attributeName = NormalizeAttributeName(attribute.Name.ToString());
                if (string.IsNullOrWhiteSpace(attributeName))
                {
                    continue;
                }

                var attributeTarget = GetAttributeTarget(attribute);
                var args = attribute.ArgumentList?.Arguments;
                if (args == null || args.Value.Count == 0)
                {
                    continue;
                }

                // First pass: named arguments (LabelResource = ..., HelpResource = ..., etc.)
                for (var i = 0; i < args.Value.Count; i++)
                {
                    String propertyName;
                    var arg = args.Value[i];
                    var named = arg.NameEquals;
                    if (named == null)
                    {
                        var colonNamed = arg.NameColon;
                        if (colonNamed == null)
                        {
                            Console.WriteLine($"1 AttrRaw: {attribute.Name} → Normalized: {attributeName}");

                            continue;
                        }
                        else 
                            propertyName = colonNamed.Name.Identifier.Text;
                    }
                    else
                        propertyName = named.Name.Identifier.Text;

                    if (string.IsNullOrWhiteSpace(propertyName))
                    {
                        Console.WriteLine($"2 AttrRaw: {attribute.Name} → Normalized: {attributeName}");

                        continue;
                    }

                    if (!AttributeUsageMap.TryGetValue((attributeName, propertyName), out var usageKind))
                    {
                        Console.WriteLine($"3 AttrRaw: {attribute.Name} → Normalized: {attributeName} PropertyName: {propertyName} Usage Kind: {usageKind}");

                        continue; // not a mapped resource property
                    }

                    if (!(arg.Expression is MemberAccessExpressionSyntax memberAccess))
                    {
                        Console.WriteLine($"4 AttrRaw: {attribute.Name} → Normalized: {attributeName} Usage Kind: {usageKind}");
                        continue; // expect CommonStrings.Names.Address, etc.
                    }

                    if (!TryParseResourceMemberAccess(memberAccess, out var containerName, out var key, out var isNameConstant))
                    {
                        Console.WriteLine($"5 AttrRaw: {attribute.Name} → Normalized: {attributeName} Usage Kind: {usageKind}");
                        continue;
                    }

                    BuildAttributeUsageRecord(
                        records,
                        orgId,
                        projectId,
                        repoId,
                        relativePath,
                        attributeName,
                        propertyName,
                        usageKind,
                        memberAccess,
                        containerName,
                        key,
                        isNameConstant,
                        attributeTarget,
                        isTest);

                    Console.WriteLine($"AttrRaw: {attribute.Name} → Normalized: {attributeName} Usage Kind: {usageKind}");

                }


                // Second pass: positional arguments for specific attributes.
                for (var i = 0; i < args.Value.Count; i++)
                {
                    var arg = args.Value[i];
                    if (arg.NameEquals != null)
                    {
                        continue; // handled above as named argument
                    }

                    if (!TryMapPositionalAttributeUsage(attributeName, i, out var usageKind, out var pseudoPropertyName))
                    {
                        continue;
                    }

                    if (!(arg.Expression is MemberAccessExpressionSyntax memberAccess))
                    {
                        continue;
                    }

                    if (!TryParseResourceMemberAccess(memberAccess, out var containerName, out var key, out var isNameConstant))
                    {
                        continue;
                    }

                    BuildAttributeUsageRecord(
                        records,
                        orgId,
                        projectId,
                        repoId,
                        relativePath,
                        attributeName,
                        pseudoPropertyName,
                        usageKind,
                        memberAccess,
                        containerName,
                        key,
                        isNameConstant,
                        attributeTarget,
                        isTest);
                }
            }
        }

        /// <summary>
        /// Maps positional arguments for known attributes to a ResourceUsageKind
        /// and a synthetic attribute property name for trace/debugging.
        /// </summary>
        private static bool TryMapPositionalAttributeUsage(
            string attributeName,
            int position,
            out ResourceUsageKind usageKind,
            out string attributePropertyName)
        {
            usageKind = ResourceUsageKind.Unknown;
            attributePropertyName = null;

            if (string.Equals(attributeName, "EntityDescription", StringComparison.OrdinalIgnoreCase))
            {
                // [EntityDescription(Title, Help, Description)] -- zero-based positions.
                switch (position)
                {
                    case 1:
                        usageKind = ResourceUsageKind.ModelTitle; // Title
                        attributePropertyName = "TitleResource";
                        return true;
                    case 2:
                        usageKind = ResourceUsageKind.ModelHelp; // User help
                        attributePropertyName = "HelpResource";
                        return true;
                    case 3:
                        usageKind = ResourceUsageKind.ModelDescription; // Description
                        attributePropertyName = "DescriptionResource";
                        return true;
                    default:
                        return false;
                }
            }

            if (string.Equals(attributeName, "EnumLabel", StringComparison.OrdinalIgnoreCase))
            {
                // [EnumLabel(TextLabel, HelpResource = ...)]
                if (position == 1)
                {
                    usageKind = ResourceUsageKind.EnumLabel;
                    attributePropertyName = "LabelResource";
                    return true;
                }

                return false;
            }

            return false;
        }

        private static void BuildAttributeUsageRecord(
            List<ResourceUsageRecord> records,
            string orgId,
            string projectId,
            string repoId,
            string relativePath,
            string attributeTypeName,
            string attributePropertyName,
            ResourceUsageKind usageKind,
            MemberAccessExpressionSyntax memberAccess,
            string containerName,
            string resourceKey,
            bool isNameConstant,
            AttributeTargetInfo targetInfo,
            bool isTest)
        {
            var record = new ResourceUsageRecord
            {
                OrgId = orgId,
                ProjectId = projectId,
                RepoId = repoId,

                ResourceContainerShortName = containerName,
                ResourceContainerFullName = containerName, // can be enriched upstream
                ResourceKey = resourceKey,
                Culture = string.Empty,

                RelativePath = relativePath,
                SymbolName = targetInfo.SymbolName,
                SymbolFullName = targetInfo.SymbolFullName,
                SymbolKind = targetInfo.SymbolKind,
                SubKind = null,

                TargetModelFullName = targetInfo.TargetModelFullName,
                TargetModelPropertyName = targetInfo.TargetModelPropertyName,

                AttributeTypeName = attributeTypeName,
                AttributePropertyName = attributePropertyName,

                UsageKind = usageKind,
                IsNameConstant = isNameConstant,
                IsTestCode = isTest,

                UsageContextSnippet = memberAccess.ToString(),
                UsagePattern = "Attribute"
            };

            records.Add(record);
        }

        private static AttributeTargetInfo GetAttributeTarget(AttributeSyntax attribute)
        {
            // AttributeList -> parent is usually a member declaration (class, property, etc.)
            var list = attribute.Parent as AttributeListSyntax;
            var owner = list?.Parent;

            string ns = null;
            string typeName = null;
            string fullTypeName = null;
            string propertyName = null;
            string symbolKind = null;

            // Walk up to find containing type and namespace.
            var typeDeclaration = owner?.FirstAncestorOrSelf<TypeDeclarationSyntax>();
            if (typeDeclaration != null)
            {
                typeName = typeDeclaration.Identifier.Text;

                var nsNode = typeDeclaration.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
                if (nsNode != null)
                {
                    ns = nsNode.Name.ToString();
                }
                else
                {
                    var fileScopedNs = typeDeclaration.FirstAncestorOrSelf<FileScopedNamespaceDeclarationSyntax>();
                    if (fileScopedNs != null)
                    {
                        ns = fileScopedNs.Name.ToString();
                    }
                }

                fullTypeName = !string.IsNullOrWhiteSpace(ns) ? $"{ns}.{typeName}" : typeName;
            }

            switch (owner)
            {
                case PropertyDeclarationSyntax propDecl:
                    propertyName = propDecl.Identifier.Text;
                    symbolKind = "Property";
                    break;

                case ClassDeclarationSyntax classDecl:
                case StructDeclarationSyntax sructDecl    :
                case RecordDeclarationSyntax recordDecl:
                    symbolKind = "Type";
                    break;

                default:
                    symbolKind = owner?.Kind().ToString();
                    break;
            }

            var symbolName = propertyName ?? typeName;
            var symbolFullName = propertyName != null && fullTypeName != null
                ? $"{fullTypeName}.{propertyName}"
                : fullTypeName;

            return new AttributeTargetInfo
            {
                TargetModelFullName = fullTypeName,
                TargetModelPropertyName = propertyName,
                SymbolName = symbolName,
                SymbolFullName = symbolFullName,
                SymbolKind = symbolKind
            };
        }

        private sealed class AttributeTargetInfo
        {
            public string TargetModelFullName { get; set; }
            public string TargetModelPropertyName { get; set; }
            public string SymbolName { get; set; }
            public string SymbolFullName { get; set; }
            public string SymbolKind { get; set; }
        }

        private static string NormalizeAttributeName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return null;
            }

            var name = rawName.Trim();

            // Strip generic arguments, e.g. SomethingAttribute<T> -> SomethingAttribute
            var tickIndex = name.IndexOf('<');
            if (tickIndex >= 0)
            {
                name = name.Substring(0, tickIndex);
            }

            // Strip trailing "Attribute" if present
            if (name.EndsWith("Attribute", StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - "Attribute".Length);
            }

            return name;
        }

        #endregion

        #region Member-access detection (non-attribute usage)

        private static void DetectMemberAccessUsages(
            SyntaxNode root,
            List<ResourceUsageRecord> records,
            string orgId,
            string projectId,
            string repoId,
            string relativePath,
            bool isTest)
        {
            var memberAccesses = root.DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>();

            foreach (var memberAccess in memberAccesses)
            {
                if (!TryParseResourceMemberAccess(memberAccess, out var containerName, out var key, out var isNameConstant))
                {
                    continue;
                }

                if (!IsLikelyResourceContainerName(containerName))
                {
                    continue;
                }

                // For non-attribute usages we only fill basic symbol info (best-effort).
                var targetInfo = GetEnclosingSymbolInfo(memberAccess);

                var targetProporetyName = targetInfo.SymbolName;

                if (records.Any(r =>
                    r.RelativePath == relativePath &&
                    r.ResourceContainerShortName == containerName &&
                    r.ResourceKey == key &&
                    r.AttributeTypeName != null))
                {
                    continue;
                }

                var record = new ResourceUsageRecord
                {
                    OrgId = orgId,
                    ProjectId = projectId,
                    RepoId = repoId,

                    ResourceContainerShortName = containerName,
                    ResourceContainerFullName = containerName,
                    ResourceKey = key,
                    Culture = string.Empty,

                    RelativePath = relativePath,
                    SymbolName = targetInfo.SymbolName,
                    SymbolFullName = targetInfo.SymbolFullName,
                    SymbolKind = targetInfo.SymbolKind,
                    SubKind = null,

                    TargetModelFullName = targetInfo.TargetModelFullName,
                    TargetModelPropertyName = targetInfo.TargetModelPropertyName,

                    AttributeTypeName = null,
                    AttributePropertyName = null,

                    UsageKind = ResourceUsageKind.Unknown,
                    IsNameConstant = isNameConstant,
                    IsTestCode = isTest,

                    UsageContextSnippet = memberAccess.ToString(),
                    UsagePattern = isNameConstant ? "NamesConstant" : "ResourceProperty"
                };

                records.Add(record);
            }
        }


        private sealed class EnclosingSymbolInfo
        {
            public string TargetModelFullName { get; set; }
            public string TargetModelPropertyName { get; set; }
            public string SymbolName { get; set; }
            public string SymbolFullName { get; set; }
            public string SymbolKind { get; set; }
        }

        private static EnclosingSymbolInfo GetEnclosingSymbolInfo(SyntaxNode node)
        {
            // Walk up to nearest property / method / type to build a friendly symbol description.
            var propertyDecl = node.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
            var methodDecl = node.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();
            var typeDecl = node.FirstAncestorOrSelf<TypeDeclarationSyntax>();

            string ns = null;
            string typeName = null;
            string fullTypeName = null;
            string memberName = null;
            string symbolKind = null;
            string targetModelPropertyName = null;

            if (typeDecl != null)
            {
                typeName = typeDecl.Identifier.Text;

                var nsNode = typeDecl.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
                if (nsNode != null)
                {
                    ns = nsNode.Name.ToString();
                }
                else
                {
                    var fileNs = typeDecl.FirstAncestorOrSelf<FileScopedNamespaceDeclarationSyntax>();
                    if (fileNs != null)
                    {
                        ns = fileNs.Name.ToString();
                    }
                }

                fullTypeName = !string.IsNullOrWhiteSpace(ns) ? $"{ns}.{typeName}" : typeName;
            }

            if (propertyDecl != null)
            {
                memberName = propertyDecl.Identifier.Text;
                symbolKind = "Property";
                targetModelPropertyName = memberName;
            }
            else if (methodDecl != null)
            {
                memberName = methodDecl switch
                {
                    MethodDeclarationSyntax m => m.Identifier.Text,
                    ConstructorDeclarationSyntax c => c.Identifier.Text,
                    _ => methodDecl.ToString()
                };
                symbolKind = "Method";
            }
            else if (typeDecl != null)
            {
                memberName = typeName;
                symbolKind = "Type";
            }

            var symbolName = memberName ?? typeName;
            var symbolFullName = memberName != null && fullTypeName != null
                ? $"{fullTypeName}.{memberName}"
                : fullTypeName;

            return new EnclosingSymbolInfo
            {
                TargetModelFullName = fullTypeName,
                TargetModelPropertyName = targetModelPropertyName,
                SymbolName = symbolName,
                SymbolFullName = symbolFullName,
                SymbolKind = symbolKind
            };
        }

        #endregion

        #region Shared helpers

        /// <summary>
        /// Attempts to parse a member access into a resource container and key.
        /// Handles both:
        ///   Container.Key
        ///   Container.Names.Key
        /// </summary>
        private static bool TryParseResourceMemberAccess(
     MemberAccessExpressionSyntax memberAccess,
     out string containerName,
     out string key,
     out bool isNameConstant)
        {
            containerName = null;
            key = null;
            isNameConstant = false;

            // Right-most identifier is always the key.
            if (!(memberAccess.Name is IdentifierNameSyntax keyIdentifier))
            {
                return false;
            }

            key = keyIdentifier.Identifier.Text;

            // *** THIS is the critical noise filter ***
            // We never want: Container.Names
            if (string.Equals(key, "Names", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var expr = memberAccess.Expression;

            // Case 1: Container.Key
            if (expr is IdentifierNameSyntax containerIdentifier)
            {
                containerName = containerIdentifier.Identifier.Text;
                return true;
            }

            // Case 2: Container.Names.Key
            if (expr is MemberAccessExpressionSyntax leftAccess &&
                leftAccess.Name is IdentifierNameSyntax maybeNames &&
                leftAccess.Expression is IdentifierNameSyntax containerIdentifier2)
            {
                if (!string.Equals(maybeNames.Identifier.Text, "Names", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                containerName = containerIdentifier2.Identifier.Text;
                isNameConstant = true;
                return true;
            }

            return false;
        }


        private static bool IsLikelyResourceContainerName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            // Heuristics based on your conventions.
            if (typeName.EndsWith("Resources", StringComparison.Ordinal) ||
                typeName.EndsWith("Strings", StringComparison.Ordinal))
            {
                return true;
            }

            if (typeName.IndexOf("Resource", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            return false;
        }

        private static string NormalizePath(string path)
        {
            return path?.Replace('\\', '/');
        }

        private static bool IsTestPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            var normalized = NormalizePath(relativePath);

            if (normalized.IndexOf("/tests/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (normalized.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalized.IndexOf(".tests/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        #endregion
    }
}
