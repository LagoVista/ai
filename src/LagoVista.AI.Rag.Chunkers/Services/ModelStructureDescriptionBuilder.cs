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
    /// Builds IDX-0037 ModelStructureDescription from raw C# source.
    /// Works purely from syntax (no reflection) and a resource dictionary.
    /// Also infers EntityHeader-based relationships for the structural graph,
    /// and composition-based child objects for ChildView / ChildList field types.
    /// </summary>
    public static class ModelStructureDescriptionBuilder
    {
        private static string GetAttributeArgumentName(AttributeArgumentSyntax arg)
        {
            if (arg.NameEquals != null)
                return arg.NameEquals.Name.Identifier.Text;

            if (arg.NameColon != null)
                return arg.NameColon.Name.Identifier.Text;

            return null;
        }

        /// <summary>
        /// Parse a C# model type annotated with [EntityDescription] and its [FormField] properties
        /// into a ModelStructureDescription suitable for indexing.
        /// </summary>
        public static ModelStructureDescription FromSource(
            string sourceText,
            string relativePath,
            IReadOnlyDictionary<string, string> resources)
        {
            if (sourceText == null) throw new ArgumentNullException(nameof(sourceText));
            if (resources == null) throw new ArgumentNullException(nameof(resources));

            var tree = CSharpSyntaxTree.ParseText(sourceText);
            var root = tree.GetRoot();

            // Find the primary model class (first class with [EntityDescription])
            var modelType = root
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => HasAttribute(c, "EntityDescription"));

            if (modelType == null)
                throw new InvalidOperationException("No class with [EntityDescription] attribute was found in the provided source.");

            var ns = GetNamespace(modelType);
            var modelName = modelType.Identifier.ValueText;
            var qualifiedName = string.IsNullOrWhiteSpace(ns) ? modelName : ns + "." + modelName;

            var entityAttr = modelType.AttributeLists
                .SelectMany(al => al.Attributes)
                .First(a => IsAttributeNamed(a, "EntityDescription"));

            var args = entityAttr.ArgumentList?.Arguments;
            if (args == null || args.Value.Count < 4)
                throw new InvalidOperationException("EntityDescription attribute must have at least 4 positional arguments (Domain, Title, Description, UserHelp).");

            // Positional arguments: Domain, TitleResource, DescriptionResource, UserHelpResource, ...
            var domainExpr = args.Value[0].Expression;
            var titleKey = ExtractResourceKey(args.Value[1].Expression);
            var descKey = ExtractResourceKey(args.Value[2].Expression);
            var helpKey = ExtractResourceKey(args.Value[3].Expression);

            var domain = ExtractDomainValue(domainExpr);

            var title = Lookup(resources, titleKey, "EntityDescription TitleResource");
            var description = Lookup(resources, descKey, "EntityDescription DescriptionResource");
            var help = Lookup(resources, helpKey, "EntityDescription UserHelpResource");

            // Named arguments for URLs and core affordances
            var listUIUrl = GetNamedString(entityAttr, "ListUIUrl");
            var editUIUrl = GetNamedString(entityAttr, "EditUIUrl");
            var createUIUrl = GetNamedString(entityAttr, "CreateUIUrl");
            var helpUrl = GetNamedString(entityAttr, "HelpUrl");
            var insertUrl = GetNamedString(entityAttr, "InsertUrl");
            var saveUrl = GetNamedString(entityAttr, "SaveUrl");
            var updateUrl = GetNamedString(entityAttr, "UpdateUrl");
            var factoryUrl = GetNamedString(entityAttr, "FactoryUrl");
            var getUrl = GetNamedString(entityAttr, "GetUrl");
            var getListUrl = GetNamedString(entityAttr, "GetListUrl");
            var deleteUrl = GetNamedString(entityAttr, "DeleteUrl");

            var result = new ModelStructureDescription
            {
                // Identity
                ModelName = modelName,
                Namespace = ns,
                QualifiedName = qualifiedName,
                Domain = domain,

                // UX strings
                Title = title,
                Description = description?.Trim(),
                Help = help?.Trim(),

                // Capabilities
                Cloneable = GetNamedBool(entityAttr, "Cloneable"),
                CanImport = GetNamedBool(entityAttr, "CanImport"),
                CanExport = GetNamedBool(entityAttr, "CanExport"),

                // UI / API affordances
                ListUIUrl = listUIUrl,
                EditUIUrl = editUIUrl,
                CreateUIUrl = createUIUrl,
                HelpUrl = helpUrl,
                InsertUrl = insertUrl,
                SaveUrl = saveUrl,
                UpdateUrl = updateUrl,
                FactoryUrl = factoryUrl,
                GetUrl = getUrl,
                GetListUrl = getListUrl,
                DeleteUrl = deleteUrl,

                // Structural graph
                Properties = new List<ModelPropertyDescription>(),
                EntityHeaderRefs = new List<ModelEntityHeaderRefDescription>(),
                ChildObjects = new List<ModelChildObjectDescription>(),
                Relationships = new List<ModelRelationshipDescription>()
            };

            // Pre-scan enums so we can mark simple enum usage in properties
            var enumNames = new HashSet<string>(
                root.DescendantNodes()
                    .OfType<EnumDeclarationSyntax>()
                    .Select(e => e.Identifier.ValueText),
                StringComparer.Ordinal);

            // Extract FormField-based property metadata (shape/semantics only for IDX-0037)
            foreach (var prop in modelType.Members.OfType<PropertyDeclarationSyntax>())
            {
                var formFieldAttr = prop.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .FirstOrDefault(a => IsAttributeNamed(a, "FormField"));

                // For IDX-0037 we only include properties that participate in the UI / metadata
                if (formFieldAttr == null)
                    continue;

                var propertyName = prop.Identifier.ValueText;

                // Shape analysis: CLR type & collection
                var (clrType, isCollection) = AnalyzePropertyType(prop.Type);

                // Value vs reference (heuristic; no reflection available)
                var isValueType = LooksLikeValueType(clrType);

                // Enum detection: if the (unqualified) type matches a known enum in this file
                var simpleTypeName = clrType;
                var dotIdx = simpleTypeName.LastIndexOf('.');
                if (dotIdx >= 0 && dotIdx < simpleTypeName.Length - 1)
                    simpleTypeName = simpleTypeName.Substring(dotIdx + 1);
                var isEnum = enumNames.Contains(simpleTypeName);

                // FieldType = enum argument on [FormField]
                var fieldTypeName = GetNamedEnumName(formFieldAttr, "FieldType");

                // Primary-key heuristic: name-based and FormField type-based
                var isKey =
                    string.Equals(propertyName, "Key", StringComparison.OrdinalIgnoreCase) ||
                    propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fieldTypeName, "Key", StringComparison.OrdinalIgnoreCase);

                var propDesc = new ModelPropertyDescription
                {
                    Name = propertyName,
                    ClrType = clrType,
                    IsCollection = isCollection,
                    IsValueType = isValueType,
                    IsEnum = isEnum,
                    IsKey = isKey,

                    // Wiring points – we will populate these below as we detect
                    // entity header references or child objects.
                    EntityHeaderRefKey = null,
                    ChildObjectKey = null,
                    Group = null
                };

                // -------- EntityHeader-based relationship detection --------
                // We only treat EntityHeader<T> as true external relationships.
                if (IsEntityHeaderType(clrType, out var targetTypeName))
                {
                    var refKey = ToCamelCase(propertyName);

                    // Register entity header reference
                    result.EntityHeaderRefs.Add(new ModelEntityHeaderRefDescription
                    {
                        Key = refKey,
                        PropertyName = propertyName,
                        TargetType = targetTypeName,
                        Domain = null, // can be populated in a later pass when domain metadata is available
                        IsCollection = isCollection
                    });

                    // Wire property back to the ref key
                    propDesc.EntityHeaderRefKey = refKey;

                    // Create a relationship edge in the model graph
                    var toModelSimple = GetSimpleTypeName(targetTypeName ?? "EntityHeader");
                    var relName = $"{modelName}To{toModelSimple}";

                    result.Relationships.Add(new ModelRelationshipDescription
                    {
                        Name = relName,
                        FromModel = qualifiedName,
                        ToModel = targetTypeName ?? "EntityHeader",
                        Cardinality = isCollection ? "OneToMany" : "OneToOne",
                        SourceProperty = propertyName,
                        TargetProperty = null,
                        Description = null
                    });
                }

                // -------- Child object / composition detection --------
                // Treat ChildView/ChildItem/ChildList* as composition on this model.
                if (!string.IsNullOrEmpty(fieldTypeName))
                {
                    if (string.Equals(fieldTypeName, "ChildView", StringComparison.Ordinal) ||
                        string.Equals(fieldTypeName, "ChildItem", StringComparison.Ordinal))
                    {
                        AddChildObjectComposition(
                            result,
                            resources,
                            propDesc,
                            propertyName,
                            clrType,
                            isCollection: false,
                            formFieldAttr: formFieldAttr);
                    }
                    else if (string.Equals(fieldTypeName, "ChildList", StringComparison.Ordinal) ||
                             string.Equals(fieldTypeName, "ChildListInline", StringComparison.Ordinal) ||
                             string.Equals(fieldTypeName, "ChildListInlinePicker", StringComparison.Ordinal))
                    {
                        AddChildObjectComposition(
                            result,
                            resources,
                            propDesc,
                            propertyName,
                            clrType,
                            isCollection: true,
                            formFieldAttr: formFieldAttr);
                    }
                }

                result.Properties.Add(propDesc);
            }

            // EntityHeaderRefs and Relationships are currently derived solely from
            // EntityHeader<T> properties. ChildObjects are treated as composition
            // and not exposed as separate relationships at this stage.

            return result;
        }

        // ---------- helpers ----------

        private static string Lookup(IReadOnlyDictionary<string, string> resources, string key, string context)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            if (!resources.TryGetValue(key, out var value))
                throw new KeyNotFoundException(
                    $"Resource key '{key}' required for {context} was not found in the provided dictionary.");

            return value;
        }

        private static bool HasAttribute(ClassDeclarationSyntax type, string name)
        {
            return type.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => IsAttributeNamed(a, name));
        }

        private static bool IsAttributeNamed(AttributeSyntax attr, string simpleName)
        {
            if (attr?.Name == null) return false;

            var raw = attr.Name.ToString();

            // Strip namespace
            var dotIndex = raw.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex < raw.Length - 1)
                raw = raw.Substring(dotIndex + 1);

            // Strip trailing "Attribute"
            if (raw.EndsWith("Attribute", StringComparison.Ordinal))
                raw = raw.Substring(0, raw.Length - "Attribute".Length);

            return string.Equals(raw, simpleName, StringComparison.Ordinal);
        }

        private static string GetNamespace(SyntaxNode node)
        {
            var current = node;
            while (current != null)
            {
                if (current is NamespaceDeclarationSyntax nds)
                    return nds.Name.ToString();
                if (current is FileScopedNamespaceDeclarationSyntax fnds)
                    return fnds.Name.ToString();

                current = current.Parent;
            }

            return null;
        }

        private static string GetNamedString(AttributeSyntax attr, string name)
        {
            var arg = attr.ArgumentList?.Arguments
                .FirstOrDefault(a => GetAttributeArgumentName(a) == name);

            if (arg == null) return null;

            if (arg.Expression is LiteralExpressionSyntax lit &&
                lit.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return lit.Token.ValueText;
            }

            return null;
        }

        private static bool GetNamedBool(AttributeSyntax attr, string name)
        {
            var arg = attr.ArgumentList?.Arguments
                .FirstOrDefault(a => GetAttributeArgumentName(a) == name);

            if (arg == null) return false;

            if (arg.Expression is LiteralExpressionSyntax lit)
            {
                if (lit.IsKind(SyntaxKind.TrueLiteralExpression)) return true;
                if (lit.IsKind(SyntaxKind.FalseLiteralExpression)) return false;
            }

            return false;
        }

        private static string GetNamedEnumName(AttributeSyntax attr, string name)
        {
            var arg = attr.ArgumentList?.Arguments
                .FirstOrDefault(a => GetAttributeArgumentName(a) == name);

            if (arg == null) return null;

            var expr = arg.Expression;

            return expr switch
            {
                MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                IdentifierNameSyntax id => id.Identifier.Text,
                _ => expr.ToString()
            };
        }

        private static string GetNamedResourceKey(AttributeSyntax attr, string name)
        {
            var arg = attr.ArgumentList?.Arguments
                .FirstOrDefault(a => GetAttributeArgumentName(a) == name);

            if (arg == null) return null;

            return ExtractResourceKey(arg.Expression);
        }

        /// <summary>
        /// Extract a resource key from an attribute expression.
        /// Handles patterns like:
        ///   - "Common_Name"
        ///   - AIResources.Names.Common_Name
        ///   - SomeNamespace.Resources.Names.AgentContext_Title
        /// We always take the segment AFTER ".Names." when present.
        /// </summary>
        private static string ExtractResourceKey(ExpressionSyntax expr)
        {
            switch (expr)
            {
                case LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression):
                    return lit.Token.ValueText;

                case IdentifierNameSyntax id:
                    return id.Identifier.Text;

                case MemberAccessExpressionSyntax ma:
                    {
                        var full = ma.ToString();
                        var idx = full.IndexOf(".Names.", StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            var start = idx + ".Names.".Length;
                            if (start < full.Length)
                                return full.Substring(start);
                        }

                        // Fallback: last identifier
                        return ma.Name.Identifier.Text;
                    }

                default:
                    {
                        var text = expr.ToString();
                        var idx = text.IndexOf(".Names.", StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            var start = idx + ".Names.".Length;
                            if (start < text.Length)
                                return text.Substring(start);
                        }

                        return text;
                    }
            }
        }

        /// <summary>
        /// Domain argument can be a string literal or something like AIDomain.AIAdmin.
        /// For the latter we take the final identifier segment (e.g., "AIAdmin").
        /// </summary>
        private static string ExtractDomainValue(ExpressionSyntax expr)
        {
            return expr switch
            {
                LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression)
                    => lit.Token.ValueText,
                MemberAccessExpressionSyntax ma
                    => ma.Name.Identifier.Text,
                IdentifierNameSyntax id
                    => id.Identifier.Text,
                _ => expr.ToString()
            };
        }

        /// <summary>
        /// Analyze the CLR type string and whether this is a collection.
        /// Handles simple types, generics like List&lt;T&gt;, and arrays.
        /// </summary>
        private static (string clrType, bool isCollection) AnalyzePropertyType(TypeSyntax typeSyntax)
        {
            switch (typeSyntax)
            {
                case ArrayTypeSyntax ats:
                    return (ats.ElementType.ToString(), true);

                case GenericNameSyntax gns:
                    {
                        var name = gns.Identifier.ValueText;
                        var isCollection = string.Equals(name, "List", StringComparison.Ordinal) ||
                                           string.Equals(name, "IList", StringComparison.Ordinal) ||
                                           string.Equals(name, "ICollection", StringComparison.Ordinal) ||
                                           string.Equals(name, "IEnumerable", StringComparison.Ordinal);

                        if (isCollection && gns.TypeArgumentList.Arguments.Count == 1)
                        {
                            var inner = gns.TypeArgumentList.Arguments[0].ToString();
                            return (inner, true);
                        }

                        return (typeSyntax.ToString(), isCollection);
                    }

                default:
                    return (typeSyntax.ToString(), false);
            }
        }

        /// <summary>
        /// Very simple heuristic for value types without reflection.
        /// </summary>
        private static bool LooksLikeValueType(string clrType)
        {
            if (string.IsNullOrWhiteSpace(clrType)) return false;

            var simple = clrType;
            var dot = simple.LastIndexOf('.');
            if (dot >= 0 && dot < simple.Length - 1)
                simple = simple.Substring(dot + 1);

            switch (simple)
            {
                case "bool":
                case "byte":
                case "sbyte":
                case "short":
                case "ushort":
                case "int":
                case "uint":
                case "long":
                case "ulong":
                case "float":
                case "double":
                case "decimal":
                case "Guid":
                case "DateTime":
                case "TimeSpan":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Detects if a CLR type string represents an EntityHeader or EntityHeader&lt;T&gt;
        /// and, when generic, extracts the target type name.
        /// </summary>
        private static bool IsEntityHeaderType(string clrType, out string targetType)
        {
            targetType = null;
            if (string.IsNullOrWhiteSpace(clrType)) return false;

            var text = clrType.Trim();

            // Look for EntityHeader<...> (fully-qualified or not)
            var idx = text.IndexOf("EntityHeader<", StringComparison.Ordinal);
            if (idx >= 0)
            {
                var start = idx + "EntityHeader<".Length;
                var end = text.IndexOf('>', start);
                if (end > start)
                {
                    targetType = text.Substring(start, end - start).Trim();
                }
                return true;
            }

            // Non-generic EntityHeader (we keep targetType as null)
            if (text.EndsWith("EntityHeader", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private static void AddChildObjectComposition(
            ModelStructureDescription result,
            IReadOnlyDictionary<string, string> resources,
            ModelPropertyDescription propDesc,
            string propertyName,
            string clrType,
            bool isCollection,
            AttributeSyntax formFieldAttr)
        {
            // If we've already registered a child object for this property, just wire the key.
            var existing = result.ChildObjects.FirstOrDefault(c => c.PropertyName == propertyName);
            if (existing != null)
            {
                propDesc.ChildObjectKey = existing.Key;
                return;
            }

            var key = ToCamelCase(propertyName);

            // Title/Description from label/help resources when available
            string title = null;
            string description = null;

            var labelKey = GetNamedResourceKey(formFieldAttr, "LabelResource");
            if (!string.IsNullOrWhiteSpace(labelKey))
            {
                title = Lookup(resources, labelKey, $"FormField LabelResource for {propertyName}");
            }

            var helpKey = GetNamedResourceKey(formFieldAttr, "HelpResource");
            if (!string.IsNullOrWhiteSpace(helpKey))
            {
                description = Lookup(resources, helpKey, $"FormField HelpResource for {propertyName}");
            }

            var child = new ModelChildObjectDescription
            {
                Key = key,
                PropertyName = propertyName,
                ClrType = clrType,
                IsCollection = isCollection,
                Title = title,
                Description = description
            };

            result.ChildObjects.Add(child);
            propDesc.ChildObjectKey = key;
        }

        private static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (char.IsLower(name[0])) return name;
            if (name.Length == 1) return name.ToLowerInvariant();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static string GetSimpleTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return typeName;

            var simple = typeName;

            // Strip generic arguments if present
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
