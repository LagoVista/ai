// ============================================================================
// EntityHeaderFormFieldIndexBuilder.cs
// Finds EntityHeader-like properties with [FormField] and extracts FieldType + EntityHeaderPickerUrl.
// Outputs CSV.
// ============================================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityHeaderIndexBuilder;

internal static class EntityHeaderFormFieldIndexBuilder
{
    public static int Run(string root, string outPath)
    {
        Console.WriteLine($"Mode    : EntityHeader FormField Index");
        Console.WriteLine($"Scanning: {root}");
        Console.WriteLine($"Output  : {outPath}");

        var files = ScanCommon.EnumerateCsFiles(root).ToList();
        Console.WriteLine($"Files   : {files.Count:n0}");

        var syntaxTrees = new List<SyntaxTree>(capacity: files.Count);
        var idx = 0;

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Latest), path: file);
            syntaxTrees.Add(tree);

            if (++idx % 250 == 0)
                Console.WriteLine($"Parsed {idx:n0}/{files.Count:n0} files...");
        }

        var compilation = CSharpCompilation.Create(
            assemblyName: "EntityHeaderIndexBuilder.Scan",
            syntaxTrees: syntaxTrees,
            references: ScanCommon.GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        using var fs = File.Create(outPath);
        using var sw = new StreamWriter(fs);

        sw.WriteLine(string.Join(",",
            "RelativePath",
            "FullyQualifiedClassName",
            "ClassName",
            "PropertyName",
            "PropertyType",
            "GenericArg0Type",
            "GenericArg0IsEnum",
            "FieldType",
            "EntityHeaderPickerUrl"
        ));

        var matchCount = 0;
        idx = 0;

        foreach (var tree in syntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            var rootNode = tree.GetRoot();

            foreach (var prop in rootNode.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                if (!TryGetFormFieldAttributeSyntax(prop, out var formFieldAttrSyntax))
                    continue;

                var propSymbol = semanticModel.GetDeclaredSymbol(prop) as IPropertySymbol;
                if (propSymbol == null) continue;

                // Only entity header-like properties
                if (!IsEntityHeaderLike(propSymbol.Type))
                    continue;

                // Exclude EntityHeader<Enum>
                if (propSymbol.Type is INamedTypeSymbol nh &&
                    nh.IsGenericType &&
                    nh.Name == "EntityHeader" &&
                    nh.TypeArguments.Length == 1 &&
                    IsEnumLike(nh.TypeArguments[0]))
                {
                    continue;
                }

                var row = BuildRow(root, tree.FilePath, propSymbol, formFieldAttrSyntax);
                sw.WriteLine(string.Join(",",
                    ScanCommon.Csv(row.RelativePath),
                    ScanCommon.Csv(row.FullyQualifiedClassName),
                    ScanCommon.Csv(row.ClassName),
                    ScanCommon.Csv(row.PropertyName),
                    ScanCommon.Csv(row.PropertyType),
                    ScanCommon.Csv(row.GenericArg0Type),
                    ScanCommon.Csv(row.GenericArg0IsEnum),
                    ScanCommon.Csv(row.FieldType),
                    ScanCommon.Csv(row.EntityHeaderPickerUrl)
                ));

                matchCount++;
            }

            if (++idx % 250 == 0)
                Console.WriteLine($"Processed {idx:n0}/{files.Count:n0} files...");
        }

        Console.WriteLine($"Matches : {matchCount:n0}");
        Console.WriteLine("Done.");
        return 0;
    }

    private static bool TryGetFormFieldAttributeSyntax(PropertyDeclarationSyntax prop, out AttributeSyntax formFieldAttrSyntax)
    {
        formFieldAttrSyntax = default!;

        foreach (var list in prop.AttributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();
                if (name.EndsWith("FormField", StringComparison.Ordinal) ||
                    name.EndsWith("FormFieldAttribute", StringComparison.Ordinal))
                {
                    formFieldAttrSyntax = attr;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsEntityHeaderLike(ITypeSymbol type)
    {
        // Unwrap Nullable<T>
        if (type is INamedTypeSymbol n0 &&
            n0.IsGenericType &&
            n0.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T &&
            n0.TypeArguments.Length == 1)
        {
            type = n0.TypeArguments[0];
        }

        if (type is INamedTypeSymbol named)
        {
            if (named.Name.Equals("EntityHeader", StringComparison.Ordinal))
                return true;

            // Collections: List<EntityHeader>, IEnumerable<EntityHeader>, etc.
            if (named.IsGenericType)
            {
                foreach (var arg in named.TypeArguments)
                {
                    if (IsEntityHeaderLike(arg))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool IsEnumLike(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum) return true;

        if (type is INamedTypeSymbol named &&
            named.IsGenericType &&
            named.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T &&
            named.TypeArguments.Length == 1)
        {
            return named.TypeArguments[0].TypeKind == TypeKind.Enum;
        }

        return false;
    }

    private static (string? FieldType, string? PickerUrl) ExtractFormFieldArgs(AttributeSyntax attr)
    {
        string? fieldType = null;
        string? pickerUrl = null;

        var args = attr.ArgumentList?.Arguments;
        if (args == null || args.Value.Count == 0)
            return (null, null);

        // FieldType is often first positional arg
        var first = args.Value[0];
        if (first.NameEquals == null && first.NameColon == null)
        {
            fieldType = SimplifyExpr(first.Expression);
        }

        foreach (var a in args.Value)
        {
            var argName =
                a.NameEquals?.Name.Identifier.ValueText ??
                a.NameColon?.Name.Identifier.ValueText;

            if (string.IsNullOrWhiteSpace(argName))
                continue;

            if (argName.Equals("EntityHeaderPickerUrl", StringComparison.Ordinal))
            {
                pickerUrl = ScanCommon.ExtractStringLiteral(a.Expression) ?? SimplifyExpr(a.Expression);
            }
            else if (argName.Equals("FieldType", StringComparison.Ordinal))
            {
                fieldType ??= SimplifyExpr(a.Expression);
            }
        }

        return (fieldType, pickerUrl);
    }

    private static string? SimplifyExpr(ExpressionSyntax expr)
    {
        return expr switch
        {
            MemberAccessExpressionSyntax ma => ma.ToString(),
            IdentifierNameSyntax id => id.Identifier.ValueText,
            _ => expr.ToString()
        };
    }

    private static IndexRow BuildRow(string rootDir, string filePath, IPropertySymbol propSymbol, AttributeSyntax formFieldAttrSyntax)
    {
        var containingType = propSymbol.ContainingType;

        var ns = containingType.ContainingNamespace?.ToDisplayString() ?? "";
        var className = containingType.Name;
        var fqcn = string.IsNullOrWhiteSpace(ns) ? className : $"{ns}.{className}";

        var relPath = Path.GetRelativePath(rootDir, filePath).Replace('\\', '/');

        var (fieldType, pickerUrl) = ExtractFormFieldArgs(formFieldAttrSyntax);

        var typeDisplay = propSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        string? genericArg = null;
        bool? genericArgIsEnum = null;

        if (propSymbol.Type is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length >= 1)
        {
            var arg0 = named.TypeArguments[0];
            genericArg = arg0.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            genericArgIsEnum = IsEnumLike(arg0);
        }

        return new IndexRow
        {
            RelativePath = relPath,
            FullyQualifiedClassName = fqcn,
            ClassName = className,
            PropertyName = propSymbol.Name,
            PropertyType = typeDisplay,
            GenericArg0Type = genericArg,
            GenericArg0IsEnum = genericArgIsEnum,
            FieldType = fieldType,
            EntityHeaderPickerUrl = pickerUrl
        };
    }

    private sealed class IndexRow
    {
        public string RelativePath { get; set; } = default!;
        public string FullyQualifiedClassName { get; set; } = default!;
        public string ClassName { get; set; } = default!;
        public string PropertyName { get; set; } = default!;
        public string PropertyType { get; set; } = default!;
        public string? GenericArg0Type { get; set; }
        public bool? GenericArg0IsEnum { get; set; }
        public string? FieldType { get; set; }
        public string? EntityHeaderPickerUrl { get; set; }
    }
}
