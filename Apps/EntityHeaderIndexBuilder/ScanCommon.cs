// ============================================================================
// ScanCommon.cs
// Shared helpers: file enumeration, CSV, namespace extraction, string literal extraction
// ============================================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EntityHeaderIndexBuilder;

internal static class ScanCommon
{
    public static IEnumerable<string> EnumerateCsFiles(string root)
    {
        var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(root, "bin"),
            Path.Combine(root, "obj"),
            Path.Combine(root, ".git"),
            Path.Combine(root, ".vs"),
            Path.Combine(root, "node_modules"),
        };

        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !ignore.Any(ig => IsUnderDirectory(f, ig)));

        static bool IsUnderDirectory(string file, string dir)
        {
            if (!Directory.Exists(dir)) return false;
            var fullFile = Path.GetFullPath(file);
            var fullDir = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullFile.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Normalizes routes for easy matching:
    /// - removes query strings
    /// - collapses multiple slashes
    /// - trims trailing slash
    /// - replaces {something} tokens with {x}
    /// - lowercases
    /// </summary>
    public static string NormalizeRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route)) return "";

        var r = route.Trim();

        var q = r.IndexOf('?');
        if (q >= 0) r = r.Substring(0, q);

        r = r.Replace('\\', '/');

        // collapse slashes
        while (r.Contains("//", StringComparison.Ordinal))
            r = r.Replace("//", "/", StringComparison.Ordinal);

        // normalize tokens: {id}, {orgId} -> {x}
        r = Regex.Replace(r, @"\{[^}]+\}", "{x}");

        // trim trailing slash (but keep root "/")
        if (r.Length > 1 && r.EndsWith("/", StringComparison.Ordinal))
            r = r.TrimEnd('/');

        // ensure leading slash if it looks like a route
        if (!r.StartsWith("/", StringComparison.Ordinal) && r.Contains('/', StringComparison.Ordinal))
            r = "/" + r;

        return r.ToLowerInvariant();
    }


    public static List<MetadataReference> GetMetadataReferences()
    {
        var refs = new List<MetadataReference>();

        var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (!string.IsNullOrWhiteSpace(trusted))
        {
            foreach (var path in trusted.Split(Path.PathSeparator))
            {
                refs.Add(MetadataReference.CreateFromFile(path));
            }
        }
        else
        {
            refs.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            refs.Add(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));
        }

        return refs;
    }

    public static string GetNamespace(SyntaxNode node)
    {
        for (SyntaxNode? cur = node; cur != null; cur = cur.Parent)
        {
            if (cur is FileScopedNamespaceDeclarationSyntax f)
                return f.Name.ToString();

            if (cur is NamespaceDeclarationSyntax n)
                return n.Name.ToString();
        }

        return "";
    }

    public static string? ExtractStringLiteral(ExpressionSyntax expr)
    {
        if (expr is LiteralExpressionSyntax lit &&
            lit.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return lit.Token.ValueText;
        }

        return null;
    }

    public static string Csv(object? value)
    {
        if (value == null) return "";
        var s = value.ToString() ?? "";
        var needsQuotes = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
        if (s.Contains('"')) s = s.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{s}\"" : s;
    }
}
