// ============================================================================
// EntityDescriptionUrlIndexBuilder.cs
// Finds classes with [EntityDescription] and extracts URL-ish named args.
// Outputs CSV.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityHeaderIndexBuilder;

internal static class EntityDescriptionUrlIndexBuilder
{
    private static bool InheritsFromSummaryData(ClassDeclarationSyntax cls)
    {
        if (cls.BaseList == null)
            return false;

        foreach (var baseType in cls.BaseList.Types)
        {
            var name = baseType.Type.ToString();

            // Handles:
            //   : SummaryData
            //   : Foo.SummaryData
            //   : SummaryData<T>
            if (name.EndsWith("SummaryData", StringComparison.Ordinal) ||
                name.Contains(".SummaryData", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool InheritsFromEntityBase(ClassDeclarationSyntax cls)
    {
        if (cls.BaseList == null)
            return false;

        foreach (var baseType in cls.BaseList.Types)
        {
            var name = baseType.Type.ToString();

            // Handles:
            //   : SummaryData
            //   : Foo.SummaryData
            //   : SummaryData<T>
            if (name.EndsWith("EntityBase", StringComparison.Ordinal) ||
                name.Contains(".EntityBase", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }


    public static int Run(string root, string outCsvPath)
    {
        Console.WriteLine($"Mode    : EntityDescription URL Index");
        Console.WriteLine($"Scanning: {root}");
        Console.WriteLine($"Output  : {outCsvPath}");

        var files = ScanCommon.EnumerateCsFiles(root).ToList();
        Console.WriteLine($"Files   : {files.Count:n0}");

        Directory.CreateDirectory(Path.GetDirectoryName(outCsvPath)!);
        using var fs = File.Create(outCsvPath);
        using var sw = new StreamWriter(fs);

        sw.WriteLine(string.Join(",",
             "RelativePath",
             "FullyQualifiedClassName",
             "ClassName",
             "InheritsSummaryData",
             "InheritsEntityBase",
             "GetUrl",
             "GetListUrl",
             "FactoryUrl",
             "SaveUrl",
             "DeleteUrl",
             "PreviewUIUrl",
             "ListUIUrl",
             "EditUIUrl",
             "CreateUIUrl",
             "Icon",
             "ClusterKey"
         ));


        var idx = 0;
        var matches = 0;

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Latest), path: file);
            var rootNode = tree.GetRoot();

            foreach (var cls in rootNode.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!TryGetEntityDescriptionAttribute(cls, out var entityDescAttr))
                    continue;

                var relPath = Path.GetRelativePath(root, file).Replace('\\', '/');
                var ns = ScanCommon.GetNamespace(cls);
                var className = cls.Identifier.ValueText;
                var fqcn = string.IsNullOrWhiteSpace(ns) ? className : $"{ns}.{className}";

                var named = GetNamedAttributeArgs(entityDescAttr);

                string? getUrl = named.TryGetValue("GetUrl", out var v1) ? v1 : null;
                string? getListUrl = named.TryGetValue("GetListUrl", out var v2) ? v2 : null;
                string? factoryUrl = named.TryGetValue("FactoryUrl", out var v3) ? v3 : null;
                string? saveUrl = named.TryGetValue("SaveUrl", out var v4) ? v4 : null;
                string? deleteUrl = named.TryGetValue("DeleteUrl", out var v5) ? v5 : null;

                string? previewUIUrl = named.TryGetValue("PreviewUIUrl", out var v6) ? v6 : null;
                string? listUIUrl = named.TryGetValue("ListUIUrl", out var v7) ? v7 : null;
                string? editUIUrl = named.TryGetValue("EditUIUrl", out var v8) ? v8 : null;
                string? createUIUrl = named.TryGetValue("CreateUIUrl", out var v9) ? v9 : null;

                string? icon = named.TryGetValue("Icon", out var v10) ? v10 : null;
                string? clusterKey = named.TryGetValue("ClusterKey", out var v11) ? v11 : null;
                var inheritsSummaryData = InheritsFromSummaryData(cls);
                var inheritsEntityBase = InheritsFromEntityBase(cls);

                sw.WriteLine(string.Join(",",
                    ScanCommon.Csv(relPath),
                    ScanCommon.Csv(fqcn),
                    ScanCommon.Csv(className),
                    ScanCommon.Csv(inheritsSummaryData),
                    ScanCommon.Csv(inheritsEntityBase),
                    ScanCommon.Csv(getUrl),
                    ScanCommon.Csv(getListUrl),
                    ScanCommon.Csv(factoryUrl),
                    ScanCommon.Csv(saveUrl),
                    ScanCommon.Csv(deleteUrl),
                    ScanCommon.Csv(previewUIUrl),
                    ScanCommon.Csv(listUIUrl),
                    ScanCommon.Csv(editUIUrl),
                    ScanCommon.Csv(createUIUrl),
                    ScanCommon.Csv(icon),
                    ScanCommon.Csv(clusterKey)
                ));

                matches++;
            }

            if (++idx % 500 == 0)
                Console.WriteLine($"Processed {idx:n0}/{files.Count:n0} files...");
        }

        Console.WriteLine($"Matches : {matches:n0}");
        Console.WriteLine("Done.");
        return 0;
    }

    private static bool TryGetEntityDescriptionAttribute(ClassDeclarationSyntax cls, out AttributeSyntax attrSyntax)
    {
        attrSyntax = default!;

        foreach (var list in cls.AttributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();
                if (name.EndsWith("EntityDescription", StringComparison.Ordinal) ||
                    name.EndsWith("EntityDescriptionAttribute", StringComparison.Ordinal))
                {
                    attrSyntax = attr;
                    return true;
                }
            }
        }

        return false;
    }

    private static Dictionary<string, string> GetNamedAttributeArgs(AttributeSyntax attr)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);

        var args = attr.ArgumentList?.Arguments;
        if (args == null) return dict;

        foreach (var a in args.Value)
        {
            var name =
                a.NameEquals?.Name.Identifier.ValueText ??
                a.NameColon?.Name.Identifier.ValueText;

            if (string.IsNullOrWhiteSpace(name))
                continue;

            var val =
                ScanCommon.ExtractStringLiteral(a.Expression) ??
                a.Expression.ToString();

            dict[name] = val;
        }

        return dict;
    }
}
