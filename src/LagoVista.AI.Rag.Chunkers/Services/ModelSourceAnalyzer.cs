using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// Normalized view of a model type and its FormField-based properties,
    /// derived purely from C# source and resource keys.
    /// This is intended to be reused by IDX-0037 (structure) and IDX-0038 (metadata).
    /// </summary>
    public sealed class ModelSourceAnalysisResult
    {
        // ----- Identity / domain -----
        public string ModelName { get; set; }
        public string Namespace { get; set; }
        public string QualifiedName { get; set; }
        public string Domain { get; set; }

        // ----- EntityDescription UX strings (resolved via resources) -----
        public string Title { get; set; }
        public string Description { get; set; }
        public string Help { get; set; }

        // Also keep the raw resource keys if we ever want them
        public string TitleResourceKey { get; set; }
        public string DescriptionResourceKey { get; set; }
        public string HelpResourceKey { get; set; }

        // ----- Capabilities -----
        public bool Cloneable { get; set; }
        public bool CanImport { get; set; }
        public bool CanExport { get; set; }

        // ----- UI / API affordances -----
        public string ListUIUrl { get; set; }
        public string EditUIUrl { get; set; }
        public string CreateUIUrl { get; set; }
        public string HelpUrl { get; set; }
        public string InsertUrl { get; set; }
        public string SaveUrl { get; set; }
        public string UpdateUrl { get; set; }
        public string FactoryUrl { get; set; }
        public string GetUrl { get; set; }
        public string GetListUrl { get; set; }
        public string DeleteUrl { get; set; }

        // ----- Structural / UI fields -----
        public List<FormFieldSyntaxInfo> Fields { get; } = new List<FormFieldSyntaxInfo>();
    }

    /// <summary>
    /// Syntax-level view of a [FormField] property on the model.
    /// This is intentionally richer than IDX-0037 needs so that IDX-0038 can
    /// use the same info for UI/validation metadata.
    /// </summary>
    public sealed class FormFieldSyntaxInfo
    {
        public string PropertyName { get; set; }

        // Type info
        public string ClrType { get; set; }
        public bool IsCollection { get; set; }
        public bool IsValueType { get; set; }
        public bool IsEnum { get; set; }

        // FormFieldAttribute core
        public string FieldTypeName { get; set; }        // e.g. "Text", "Key", "EntityHeaderPicker"
        public string EnumTypeName { get; set; }         // e.g. "LlmProviders" (simple name)

        // Resource keys – already normalized to the "Common_Name" style
        public string LabelResourceKey { get; set; }
        public string HelpResourceKey { get; set; }
        public string WatermarkResourceKey { get; set; }

        // Simple key heuristic we already use in IDX-0037
        public bool IsKey { get; set; }

        // Hook points for later: grouping / semantics
        public string Group { get; set; }
    }

    /// <summary>
    /// Shared analyzer for EntityDescription + FormField-based models.
    /// This encapsulates the Roslyn and resource-key logic so that
    /// multiple builders (IDX-0037, IDX-0038, etc.) can reuse it.
    /// </summary>
    public static class ModelSourceAnalyzer
    {
        public static ModelSourceAnalysisResult Analyze(
            string sourceText,
            string relativePath,
            IReadOnlyDictionary<string, string> resources)
        {
            if (sourceText == null) throw new ArgumentNullException(nameof(sourceText));
            if (resources == null) throw new ArgumentNullException(nameof(resources));

            var tree = CSharpSyntaxTree.ParseText(sourceText);
            var root = tree.GetRoot();

            // 1) Find the primary model class (same rule we used before)
            var modelType = root
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => HasAttribute(c, "EntityDescription"));

            if (modelType == null)
                throw new InvalidOperationException(
                    "No class with [EntityDescription] attribute was found in the provided source.");

            var ns = GetNamespace(modelType);
            var modelName = modelType.Identifier.ValueText;
            var qualifiedName = string.IsNullOrWhiteSpace(ns) ? modelName : ns + "." + modelName;

            // 2) EntityDescription attribute: domain + title/desc/help + URLs/flags
            var entityAttr = modelType.AttributeLists
                .SelectMany(al => al.Attributes)
                .First(a => IsAttributeNamed(a, "EntityDescription"));

            var args = entityAttr.ArgumentList?.Arguments;
            if (args == null || args.Value.Count < 4)
                throw new InvalidOperationException(
                    "EntityDescription attribute must have at least 4 positional arguments (Domain, Title, Description, UserHelp).");

            var domainExpr = args.Value[0].Expression;
            var titleKey = ExtractResourceKey(args.Value[1].Expression);
            var descKey = ExtractResourceKey(args.Value[2].Expression);
            var helpKey = ExtractResourceKey(args.Value[3].Expression);

            var domain = ExtractDomainValue(domainExpr);

            var title = Lookup(resources, titleKey, "EntityDescription TitleResource");
            var description = Lookup(resources, descKey, "EntityDescription DescriptionResource");
            var help = Lookup(resources, helpKey, "EntityDescription UserHelpResource");

            var result = new ModelSourceAnalysisResult
            {
                ModelName = modelName,
                Namespace = ns,
                QualifiedName = qualifiedName,
                Domain = domain,

                Title = title?.Trim(),
                Description = description?.Trim(),
                Help = help?.Trim(),

                TitleResourceKey = titleKey,
                DescriptionResourceKey = descKey,
                HelpResourceKey = helpKey,

                Cloneable = GetNamedBool(entityAttr, "Cloneable"),
                CanImport = GetNamedBool(entityAttr, "CanImport"),
                CanExport = GetNamedBool(entityAttr, "CanExport"),

                ListUIUrl = GetNamedString(entityAttr, "ListUIUrl"),
                EditUIUrl = GetNamedString(entityAttr, "EditUIUrl"),
                CreateUIUrl = GetNamedString(entityAttr, "CreateUIUrl"),
                HelpUrl = GetNamedString(entityAttr, "HelpUrl"),
                InsertUrl = GetNamedString(entityAttr, "InsertUrl"),
                SaveUrl = GetNamedString(entityAttr, "SaveUrl"),
                UpdateUrl = GetNamedString(entityAttr, "UpdateUrl"),
                FactoryUrl = GetNamedString(entityAttr, "FactoryUrl"),
                GetUrl = GetNamedString(entityAttr, "GetUrl"),
                GetListUrl = GetNamedString(entityAttr, "GetListUrl"),
                DeleteUrl = GetNamedString(entityAttr, "DeleteUrl"),
            };

            // 3) Pre-scan enums so we can detect enum-backed fields
            var enumNames = new HashSet<string>(
                root.DescendantNodes()
                    .OfType<EnumDeclarationSyntax>()
                    .Select(e => e.Identifier.ValueText),
                StringComparer.Ordinal);

            // 4) Walk properties with [FormField] and collect syntax-level info
            foreach (var prop in modelType.Members.OfType<PropertyDeclarationSyntax>())
            {
                var formFieldAttr = prop.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .FirstOrDefault(a => IsAttributeNamed(a, "FormField"));

                if (formFieldAttr == null)
                    continue;

                var propertyName = prop.Identifier.ValueText;
                var (clrType, isCollection) = AnalyzePropertyType(prop.Type);
                var isValueType = LooksLikeValueType(clrType);

                var simpleTypeName = GetSimpleTypeName(clrType);
                var isEnum = enumNames.Contains(simpleTypeName);

                var fieldTypeName = GetNamedEnumName(formFieldAttr, "FieldType");
                var enumTypeName = GetNamedTypeName(formFieldAttr, "EnumType");

                var labelKey = GetNamedResourceKey(formFieldAttr, "LabelResource");
                var helpKeyField = GetNamedResourceKey(formFieldAttr, "HelpResource");
                var watermarkKeyField = GetNamedResourceKey(formFieldAttr, "WaterMark");

                var isKey =
                    string.Equals(propertyName, "Key", StringComparison.OrdinalIgnoreCase) ||
                    propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fieldTypeName, "Key", StringComparison.OrdinalIgnoreCase);

                result.Fields.Add(new FormFieldSyntaxInfo
                {
                    PropertyName = propertyName,
                    ClrType = clrType,
                    IsCollection = isCollection,
                    IsValueType = isValueType,
                    IsEnum = isEnum,
                    FieldTypeName = fieldTypeName,
                    EnumTypeName = enumTypeName,
                    LabelResourceKey = labelKey,
                    HelpResourceKey = helpKeyField,
                    WatermarkResourceKey = watermarkKeyField,
                    IsKey = isKey,
                    Group = null
                });
            }

            return result;
        }

        // ====== helpers (mostly lifted from ModelStructureDescriptionBuilder) ======

        private static string Lookup(IReadOnlyDictionary<string, string> resources, string key, string context)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            if (!resources.TryGetValue(key, out var value))
                throw new KeyNotFoundException(
                    $"Resource key '{key}' required for {context} was not found in the provided dictionary.");

            return value;
        }

        private static bool HasAttribute(ClassDeclarationSyntax type, string name) =>
            type.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => IsAttributeNamed(a, name));

        private static bool IsAttributeNamed(AttributeSyntax attr, string simpleName)
        {
            if (attr?.Name == null) return false;

            var raw = attr.Name.ToString();

            var dotIndex = raw.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex < raw.Length - 1)
                raw = raw.Substring(dotIndex + 1);

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

        private static string GetAttributeArgumentName(AttributeArgumentSyntax arg)
        {
            if (arg.NameEquals != null)
                return arg.NameEquals.Name.Identifier.Text;
            if (arg.NameColon != null)
                return arg.NameColon.Name.Identifier.Text;
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

        private static string GetNamedTypeName(AttributeSyntax attr, string name)
        {
            var arg = attr.ArgumentList?.Arguments
                .FirstOrDefault(a => GetAttributeArgumentName(a) == name);

            if (arg == null) return null;

            var expr = arg.Expression;

            return expr switch
            {
                TypeOfExpressionSyntax tos => tos.Type.ToString(),
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

        private static bool LooksLikeValueType(string clrType)
        {
            if (string.IsNullOrWhiteSpace(clrType)) return false;

            var simple = GetSimpleTypeName(clrType);

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
