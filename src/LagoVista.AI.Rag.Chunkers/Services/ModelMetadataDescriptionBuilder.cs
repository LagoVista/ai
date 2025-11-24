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
    /// Builds IDX-0038 ModelMetadataDescription from raw C# source.
    /// Works purely from syntax (no reflection) and a resource dictionary.
    /// Reuses the same attribute / resource extraction patterns as the
    /// ModelStructureDescriptionBuilder, and additionally infers layout
    /// information from IFormDescriptor-style methods.
    /// </summary>
    public static class ModelMetadataDescriptionBuilder
    {
        // ---------- Public API ----------

        public static ModelMetadataDescription FromSource(IndexFileContext context, string sourceText, IReadOnlyDictionary<string, string> resources)
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

            var metadata = new ModelMetadataDescription
            {
                // Identity
                ModelName = modelName,
                Namespace = ns,
                Domain = domain,

                // UX strings
                Title = title?.Trim(),
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

                // Fields & Layouts
                Fields = new List<ModelFieldMetadataDescription>(),
                Layouts = new ModelFormLayouts()
            };

            metadata.SetCommonProperties(context);

            // ---------- Field metadata (FormFieldAttribute-driven) ----------

            foreach (var prop in modelType.Members.OfType<PropertyDeclarationSyntax>())
            {
                var formFieldAttr = prop.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .FirstOrDefault(a => IsAttributeNamed(a, "FormField"));

                if (formFieldAttr == null)
                    continue; // Only include fields that participate in UI metadata

                var propertyName = prop.Identifier.ValueText;

                // Resource-derived UX strings
                var labelKey = GetNamedResourceKey(formFieldAttr, "LabelResource");
                var helpKeyField = GetNamedResourceKey(formFieldAttr, "HelpResource");
                var watermarkKey = GetNamedResourceKey(formFieldAttr, "WaterMark");

                var label = labelKey != null ? Lookup(resources, labelKey, $"FormField.LabelResource for {propertyName}") : null;
                var helpField = helpKeyField != null ? Lookup(resources, helpKeyField, $"FormField.HelpResource for {propertyName}") : null;
                var watermark = watermarkKey != null ? Lookup(resources, watermarkKey, $"FormField.WaterMark for {propertyName}") : null;

                // Basic type info
                var (clrType, _) = AnalyzePropertyType(prop.Type);
                var fieldTypeName = GetNamedEnumName(formFieldAttr, "FieldType");

                var isRequired = GetNamedBool(formFieldAttr, "IsRequired");

                var field = new ModelFieldMetadataDescription
                {
                    PropertyName = propertyName,
                    Label = label,
                    Help = helpField,
                    Watermark = watermark,
                    FieldType = fieldTypeName,
                    DataType = clrType,
                    IsRequired = isRequired
                    // Additional flags (visibility, layout hints, etc.) can be filled in later
                };

                metadata.Fields.Add(field);
            }

            // ---------- Layouts (IFormDescriptor* methods → ModelFormLayouts) ----------

            if (metadata.Layouts == null)
                metadata.Layouts = new ModelFormLayouts();
            if (metadata.Layouts.Form == null)
                metadata.Layouts.Form = new ModelFormLayoutColumns();
            if (metadata.Layouts.Advanced == null)
                metadata.Layouts.Advanced = new ModelFormLayoutColumns();

            // Main form columns
            var col1 = ExtractLayoutFieldNames(modelType, "GetFormFields");
            if (col1 != null && col1.Count > 0)
                metadata.Layouts.Form.Col1Fields = col1;

            var col2 = ExtractLayoutFieldNames(modelType, "GetFormFieldsCol2");
            if (col2 != null && col2.Count > 0)
                metadata.Layouts.Form.Col2Fields = col2;

            var bottom = ExtractLayoutFieldNames(modelType, "GetFormFieldsBottom");
            if (bottom != null && bottom.Count > 0)
                metadata.Layouts.Form.BottomFields = bottom;

            // Advanced layout
            var advCol1 = ExtractLayoutFieldNames(modelType, "GetAdvancedFields");
            if (advCol1 != null && advCol1.Count > 0)
                metadata.Layouts.Advanced.Col1Fields = advCol1;

            var advCol2 = ExtractLayoutFieldNames(modelType, "GetAdvancedFieldsCol2");
            if (advCol2 != null && advCol2.Count > 0)
                metadata.Layouts.Advanced.Col2Fields = advCol2;

            // Inline fields
            var inlineFields = ExtractLayoutFieldNames(modelType, "GetInlineFields");
            if (inlineFields != null && inlineFields.Count > 0)
                metadata.Layouts.InlineFields = inlineFields;

            // Mobile fields
            var mobileFields = ExtractLayoutFieldNames(modelType, "GetMobileFields");
            if (mobileFields != null && mobileFields.Count > 0)
                metadata.Layouts.MobileFields = mobileFields;

            // Simple layout
            var simpleFields = ExtractLayoutFieldNames(modelType, "GetSimpleFields");
            if (simpleFields != null && simpleFields.Count > 0)
                metadata.Layouts.SimpleFields = simpleFields;

            // Quick-create layout
            var quickCreateFields = ExtractLayoutFieldNames(modelType, "GetQuickCreateFields");
            if (quickCreateFields != null && quickCreateFields.Count > 0)
                metadata.Layouts.QuickCreateFields = quickCreateFields;

            // Additional actions
            var additionalActions = ExtractAdditionalActions(modelType, resources);
            if (additionalActions != null && additionalActions.Count > 0)
                metadata.Layouts.AdditionalActions = additionalActions;

            // Advanced TabFields and other layout variants can be layered in later.

            return metadata;
        }

        // ---------- Layout helpers ----------

        /// <summary>
        /// Extracts field names from a method that returns a List&lt;string&gt; using nameof(...)
        /// entries, such as:
        ///   return new List&lt;string&gt; { nameof(Name), nameof(Key), ... };
        /// Returns lowerCamelCase property names in declaration order.
        /// </summary>
        private static List<string> ExtractLayoutFieldNames(ClassDeclarationSyntax modelType, string methodName)
        {
            var method = modelType.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == methodName);

            if (method == null || method.Body == null)
                return null;

            var names = new List<string>();

            var invocations = method.Body
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => inv.Expression is IdentifierNameSyntax id && id.Identifier.Text == "nameof");

            foreach (var inv in invocations)
            {
                var arg = inv.ArgumentList?.Arguments.FirstOrDefault()?.Expression as IdentifierNameSyntax;
                if (arg == null) continue;
                var propName = arg.Identifier.Text;
                if (string.IsNullOrWhiteSpace(propName)) continue;
                names.Add(ToCamelCase(propName));
            }

            return names.Count == 0 ? null : names;
        }

        /// <summary>
        /// Extracts additional form actions from a GetAdditionalActions() method
        /// that returns a sequence of FormAdditionalAction object initializers.
        /// </summary>
        private static List<ModelFormAdditionalActionDescription> ExtractAdditionalActions(
            ClassDeclarationSyntax modelType,
            IReadOnlyDictionary<string, string> resources)
        {
            var method = modelType.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == "GetAdditionalActions");

            if (method == null || method.Body == null)
                return null;

            var actions = new List<ModelFormAdditionalActionDescription>();

            var objectCreations = method.Body
                .DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>();

            foreach (var creation in objectCreations)
            {
                var simpleType = GetSimpleTypeName(creation.Type.ToString());
                if (!string.Equals(simpleType, "FormAdditionalAction", StringComparison.Ordinal))
                    continue;

                var action = new ModelFormAdditionalActionDescription();

                if (creation.Initializer != null)
                {
                    foreach (var expr in creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
                    {
                        if (expr.Left is not IdentifierNameSyntax left)
                            continue;

                        var propName = left.Identifier.Text;
                        var valueExpr = expr.Right;

                        switch (propName)
                        {
                            case "Title":
                                action.Title = ExtractStringOrResource(valueExpr, resources, "FormAdditionalAction.Title");
                                break;
                            case "Icon":
                                action.Icon = ExtractStringLiteral(valueExpr);
                                break;
                            case "Help":
                                action.Help = ExtractStringOrResource(valueExpr, resources, "FormAdditionalAction.Help");
                                break;
                            case "Key":
                                action.Key = ExtractStringLiteral(valueExpr);
                                break;
                            case "ForCreate":
                                action.ForCreate = ExtractBoolLiteral(valueExpr);
                                break;
                            case "ForEdit":
                                action.ForEdit = ExtractBoolLiteral(valueExpr);
                                break;
                        }
                    }
                }

                actions.Add(action);
            }

            return actions.Count == 0 ? null : actions;
        }

        private static string ExtractStringOrResource(
            ExpressionSyntax expr,
            IReadOnlyDictionary<string, string> resources,
            string context)
        {
            // String literal: use directly
            if (expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return lit.Token.ValueText;
            }

            // Otherwise treat as resource reference
            var key = ExtractResourceKey(expr);
            return Lookup(resources, key, context);
        }

        private static string ExtractStringLiteral(ExpressionSyntax expr)
        {
            if (expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return lit.Token.ValueText;
            }

            return null;
        }

        private static bool ExtractBoolLiteral(ExpressionSyntax expr)
        {
            if (expr is LiteralExpressionSyntax lit)
            {
                if (lit.IsKind(SyntaxKind.TrueLiteralExpression)) return true;
                if (lit.IsKind(SyntaxKind.FalseLiteralExpression)) return false;
            }

            return false;
        }

        // ---------- Shared helpers (mirroring ModelStructureDescriptionBuilder) ----------

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
