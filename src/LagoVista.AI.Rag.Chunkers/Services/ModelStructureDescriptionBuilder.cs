using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LagoVista.AI.Rag.Chunkers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// Builds IDX-0037 ModelStructureDescription from raw C# source.
    /// Uses ModelSourceAnalyzer for shared parsing and then enriches the
    /// result with structural wiring (EntityHeader refs, child objects,
    /// relationships, and EntityBase properties).
    /// </summary>
    public static class ModelStructureDescriptionBuilder
    {
        public static ModelStructureDescription FromSource(
            string sourceText,
            string relativePath,
            IReadOnlyDictionary<string, string> resources)
        {
            if (sourceText == null) throw new ArgumentNullException(nameof(sourceText));
            if (resources == null) throw new ArgumentNullException(nameof(resources));

            // Core analysis (EntityDescription + FormField metadata)
            var analysis = ModelSourceAnalyzer.Analyze(sourceText, relativePath, resources);

            var result = new ModelStructureDescription
            {
                // Identity
                ModelName = analysis.ModelName,
                Namespace = analysis.Namespace,
                QualifiedName = analysis.QualifiedName,
                Domain = analysis.Domain,

                // UX strings
                Title = analysis.Title,
                Description = analysis.Description,
                Help = analysis.Help,

                // Capabilities
                Cloneable = analysis.Cloneable,
                CanImport = analysis.CanImport,
                CanExport = analysis.CanExport,

                // UI / API affordances
                ListUIUrl = analysis.ListUIUrl,
                EditUIUrl = analysis.EditUIUrl,
                CreateUIUrl = analysis.CreateUIUrl,
                HelpUrl = analysis.HelpUrl,
                InsertUrl = analysis.InsertUrl,
                SaveUrl = analysis.SaveUrl,
                UpdateUrl = analysis.UpdateUrl,
                FactoryUrl = analysis.FactoryUrl,
                GetUrl = analysis.GetUrl,
                GetListUrl = analysis.GetListUrl,
                DeleteUrl = analysis.DeleteUrl,

                Properties = new List<ModelPropertyDescription>(),
                EntityHeaderRefs = new List<ModelEntityHeaderRefDescription>(),
                ChildObjects = new List<ModelChildObjectDescription>(),
                Relationships = new List<ModelRelationshipDescription>()
            };

            // Wire per-field structure from analysis.Fields
            foreach (var field in analysis.Fields)
            {
                var propDesc = new ModelPropertyDescription
                {
                    Name = field.PropertyName,
                    ClrType = field.ClrType,
                    IsCollection = field.IsCollection,
                    IsValueType = field.IsValueType,
                    IsEnum = field.IsEnum,
                    IsKey = field.IsKey,
                    EntityHeaderRefKey = null,
                    ChildObjectKey = null,
                    Group = field.Group
                };

                // ---- EntityHeader-based relationships ----
                if (IsEntityHeaderType(field.ClrType, out var targetTypeName))
                {
                    var refKey = ToCamelCase(field.PropertyName);

                    result.EntityHeaderRefs.Add(new ModelEntityHeaderRefDescription
                    {
                        Key = refKey,
                        PropertyName = field.PropertyName,
                        TargetType = targetTypeName,
                        Domain = null,
                        IsCollection = field.IsCollection
                    });

                    propDesc.EntityHeaderRefKey = refKey;

                    var toModelSimple = GetSimpleTypeName(targetTypeName ?? "EntityHeader");
                    var relName = $"{analysis.ModelName}To{toModelSimple}";

                    result.Relationships.Add(new ModelRelationshipDescription
                    {
                        Name = relName,
                        FromModel = analysis.QualifiedName,
                        ToModel = targetTypeName ?? "EntityHeader",
                        Cardinality = field.IsCollection ? "OneToMany" : "OneToOne",
                        SourceProperty = field.PropertyName,
                        TargetProperty = null,
                        Description = null
                    });
                }

                // ---- Child composition (ChildList/ChildView/etc.) ----
                if (IsChildField(field))
                {
                    var childKey = ToCamelCase(field.PropertyName);

                    result.ChildObjects.Add(new ModelChildObjectDescription
                    {
                        Key = childKey,
                        PropertyName = field.PropertyName,
                        ClrType = field.ClrType,
                        IsCollection = field.IsCollection,
                        Title = null,
                        Description = null
                    });

                    propDesc.ChildObjectKey = childKey;
                }

                result.Properties.Add(propDesc);
            }

            // Append EntityBase properties (via reflection) when applicable
            AppendEntityBasePropertiesIfApplicable(sourceText, result);

            return result;
        }

        // ---------- helpers ----------

        private static bool IsChildField(FormFieldSyntaxInfo field)
        {
            if (string.IsNullOrWhiteSpace(field.FieldTypeName)) return false;

            switch (field.FieldTypeName)
            {
                case "ChildView":
                case "ChildItem":
                case "ChildList":
                case "ChildListInline":
                case "ChildListInlinePicker":
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

            if (text.EndsWith("EntityHeader", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
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

            var lt = simple.IndexOf('<');
            if (lt >= 0)
                simple = simple.Substring(0, lt);

            var dot = simple.LastIndexOf('.');
            if (dot >= 0 && dot < simple.Length - 1)
                simple = simple.Substring(dot + 1);

            return simple;
        }

        /// <summary>
        /// If the model class inherits from EntityBase, append its public instance
        /// properties as structural properties, unless the model already defines
        /// a property with the same name.
        /// </summary>
        private static void AppendEntityBasePropertiesIfApplicable(
            string sourceText,
            ModelStructureDescription result)
        {
            if (string.IsNullOrWhiteSpace(sourceText)) return;

            var tree = CSharpSyntaxTree.ParseText(sourceText);
            var root = tree.GetRoot();

            var modelType = root
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Any(a => IsAttributeNamed(a, "EntityDescription")));

            if (modelType == null) return;
            if (!InheritsBase(modelType, "EntityBase")) return;

            var existingNames = new HashSet<string>(
                result.Properties.Select(p => p.Name),
                StringComparer.OrdinalIgnoreCase);

            var entityBaseType = typeof(LagoVista.Core.Models.EntityBase);

            foreach (var prop in entityBaseType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!prop.CanRead || !prop.CanWrite) continue;
                if (existingNames.Contains(prop.Name)) continue;

                var type = prop.PropertyType;

                var isCollection = typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
                                   && type != typeof(string);

                var isValueType = type.IsValueType || type == typeof(string);
                var isEnum = type.IsEnum;
                var clrType = type.FullName ?? type.Name;

                var isKey = string.Equals(prop.Name, "Key", StringComparison.OrdinalIgnoreCase) ||
                            prop.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase);

                result.Properties.Add(new ModelPropertyDescription
                {
                    Name = prop.Name,
                    ClrType = clrType,
                    IsCollection = isCollection,
                    IsValueType = isValueType,
                    IsEnum = isEnum,
                    IsKey = isKey,
                    EntityHeaderRefKey = null,
                    ChildObjectKey = null,
                    Group = "EntityBase"
                });
            }
        }

        private static bool InheritsBase(ClassDeclarationSyntax type, string baseTypeName)
        {
            if (type.BaseList == null) return false;

            foreach (var bt in type.BaseList.Types)
            {
                var simple = GetSimpleTypeName(bt.Type.ToString());
                if (string.Equals(simple, baseTypeName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

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
    }
}
