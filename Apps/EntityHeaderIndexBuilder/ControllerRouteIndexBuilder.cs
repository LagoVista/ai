// ============================================================================
// ControllerRouteIndexBuilder.cs
// Scans controllers for route templates + return/parameter types.
// Focus: capture “list-ish” endpoints where a parameter is ListRequest<T>.
//
// Output CSV columns (broad, but useful):
//   RelativePath, FullyQualifiedClassName, ClassName, MethodName,
//   HttpVerb, ClassRoute, MethodRoute, FullRoute,
//   FullRouteNormalized, ReturnType, IsAsyncTask, HasListRequestParam,
//   ListRequestGenericArg0Type, ListRequestGenericArg0Name
// ============================================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EntityHeaderIndexBuilder;

internal static class ControllerRouteIndexBuilder
{
    private static (string? BodyType, string? BodyTypeName, string Confidence) ExtractPostedBodyType(MethodDeclarationSyntax method, string httpVerb)
    {
        // Only meaningful for write-ish endpoints, but we can still safely return None if absent.
        var isWrite = httpVerb is "POST" or "PUT" or "PATCH";
        if (!isWrite) return (null, null, "N/A");

        foreach (var p in method.ParameterList.Parameters)
        {
            if (HasParamAttribute(p, "FromBody"))
            {
                var t = p.Type?.ToString();
                var tt = NormalizeTypeText(t);
                return (tt, SimpleName(tt), "High");
            }
        }

        // Your convention: posted bodies always use [FromBody]
        return (null, null, "None");
    }


    private static bool HasParamAttribute(ParameterSyntax p, string shortName)
    {
        foreach (var list in p.AttributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();
                if (name.Equals(shortName, StringComparison.Ordinal) ||
                    name.EndsWith("." + shortName, StringComparison.Ordinal) ||
                    name.Equals(shortName + "Attribute", StringComparison.Ordinal) ||
                    name.EndsWith("." + shortName + "Attribute", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? NormalizeTypeText(string? typeText)
    {
        if (string.IsNullOrWhiteSpace(typeText)) return null;
        var t = typeText.Trim();

        // strip leading qualifiers commonly seen in code
        if (t.StartsWith("global::", StringComparison.Ordinal))
            t = t.Substring("global::".Length);

        return t;
    }

    private static bool LooksLikePrimitive(string typeText)
    {
        // Covers common simple types and nullable/simple
        var t = typeText.Trim().TrimEnd('?');

        return t is "string" or "bool" or "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or
               "float" or "double" or "decimal" or "Guid" or "DateTime" or "DateTimeOffset";
    }

    private static bool IsClearlyNotBody(string typeText)
    {
        // Add anything “infrastructure-y” you commonly see
        return typeText.Contains("CancellationToken", StringComparison.Ordinal) ||
               typeText.Contains("HttpContext", StringComparison.Ordinal) ||
               typeText.Contains("HttpRequest", StringComparison.Ordinal) ||
               typeText.Contains("HttpResponse", StringComparison.Ordinal) ||
               typeText.Contains("ClaimsPrincipal", StringComparison.Ordinal) ||
               typeText.Contains("ILogger", StringComparison.Ordinal) ||
               typeText.Contains("IFormFile", StringComparison.Ordinal);
    }

    private static string? SimpleName(string? full)
    {
        if (string.IsNullOrWhiteSpace(full)) return null;
        var s = full.Trim();

        var lt = s.IndexOf('<');
        if (lt > 0) s = s.Substring(0, lt);

        var lastDot = s.LastIndexOf('.');
        return lastDot >= 0 ? s.Substring(lastDot + 1) : s;
    }

    private static bool HasFromServicesParam(MethodDeclarationSyntax method)
    {
        foreach (var p in method.ParameterList.Parameters)
        {
            if (HasParamAttribute(p, "FromServices"))
                return true;
        }
        return false;
    }


    public static int Run(string root, string outCsvPath)
    {
        Console.WriteLine($"Mode    : Controller Route Index");
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
            "MethodName",
            "HttpVerb",
            "ClassRoute",
            "MethodRoute",
            "FullRoute",
            "FullRouteNormalized",
            "ReturnType",
            "IsAsyncTask",
            "HasListRequestParam",
            "HasFromBodyParam",
            "FromBodyType",
            "FromBodyTypeName",
            "HasFromServicesParam"
        ));


        var processed = 0;
        var matches = 0;

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Latest), path: file);
            var rootNode = tree.GetRoot();

            foreach (var cls in rootNode.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!IsControllerLike(cls))
                    continue;

                var ns = ScanCommon.GetNamespace(cls);
                var className = cls.Identifier.ValueText;
                var fqcn = string.IsNullOrWhiteSpace(ns) ? className : $"{ns}.{className}";
                var relPath = Path.GetRelativePath(root, file).Replace('\\', '/');

                var classRoute = ExtractRouteTemplate(cls.AttributeLists);

                foreach (var method in cls.Members.OfType<MethodDeclarationSyntax>())
                {
                    if (!TryExtractHttpVerb(method, out var verb))
                        continue;

                    var methodRoute = ExtractRouteTemplate(method.AttributeLists);
                    var fullRoute = CombineRoutes(classRoute, methodRoute);
                    var fullRouteNorm = ScanCommon.NormalizeRoute(fullRoute);

                    var returnType = method.ReturnType.ToString();
                    var isAsyncTask = IsTaskLike(returnType);

                    var (hasListReq, listReqArgType, listReqArgName) = ExtractListRequestParam(method);
                    var (postedBodyType, postedBodyTypeName, postedConfidence) = ExtractPostedBodyType(method, verb);
                    var hasFromBody = postedConfidence == "High";
                    var hasFromServices = HasFromServicesParam(method);

                    sw.WriteLine(string.Join(",",
                        ScanCommon.Csv(relPath),
                        ScanCommon.Csv(fqcn),
                        ScanCommon.Csv(className),
                        ScanCommon.Csv(method.Identifier.ValueText),
                        ScanCommon.Csv(verb),
                        ScanCommon.Csv(classRoute),
                        ScanCommon.Csv(methodRoute),
                        ScanCommon.Csv(fullRoute),
                        ScanCommon.Csv(fullRouteNorm),
                        ScanCommon.Csv(returnType),
                        ScanCommon.Csv(isAsyncTask),
                        ScanCommon.Csv(hasListReq),
                        ScanCommon.Csv(hasFromBody),
                        ScanCommon.Csv(postedBodyType),
                        ScanCommon.Csv(postedBodyTypeName),
                        ScanCommon.Csv(hasFromServices)
                     ));

                    matches++;
                }
            }

            if (++processed % 500 == 0)
                Console.WriteLine($"Processed {processed:n0}/{files.Count:n0} files...");
        }

        Console.WriteLine($"Routes   : {matches:n0}");
        Console.WriteLine("Done.");
        return 0;
    }

    // ------------------------------------------------------------------------
    // Detection helpers
    // ------------------------------------------------------------------------

    private static bool IsControllerLike(ClassDeclarationSyntax cls)
    {
        // 1) Name ends with Controller (common pattern)
        if (cls.Identifier.ValueText.EndsWith("Controller", StringComparison.Ordinal))
            return true;

        // 2) [ApiController] attribute
        if (HasAttribute(cls.AttributeLists, "ApiController"))
            return true;

        // 3) Inherits from something with Controller in the name (syntax best-effort)
        if (cls.BaseList != null)
        {
            foreach (var bt in cls.BaseList.Types)
            {
                var s = bt.Type.ToString();
                if (s.EndsWith("Controller", StringComparison.Ordinal) ||
                    s.EndsWith("ControllerBase", StringComparison.Ordinal) ||
                    s.Contains(".Controller", StringComparison.Ordinal) ||
                    s.Contains(".ControllerBase", StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    private static bool HasAttribute(SyntaxList<AttributeListSyntax> lists, string attributeShortName)
    {
        foreach (var list in lists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();
                if (name.Equals(attributeShortName, StringComparison.Ordinal) ||
                    name.EndsWith(attributeShortName, StringComparison.Ordinal) ||
                    name.EndsWith(attributeShortName + "Attribute", StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    private static bool TryExtractHttpVerb(MethodDeclarationSyntax method, out string verb)
    {
        // Default: not a route method.
        verb = "";

        foreach (var list in method.AttributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();

                // Handle [HttpGet], [HttpPost], etc. and qualified names.
                if (EndsWithAttr(name, "HttpGet")) { verb = "GET"; return true; }
                if (EndsWithAttr(name, "HttpPost")) { verb = "POST"; return true; }
                if (EndsWithAttr(name, "HttpPut")) { verb = "PUT"; return true; }
                if (EndsWithAttr(name, "HttpPatch")) { verb = "PATCH"; return true; }
                if (EndsWithAttr(name, "HttpDelete")) { verb = "DELETE"; return true; }
                if (EndsWithAttr(name, "HttpHead")) { verb = "HEAD"; return true; }
                if (EndsWithAttr(name, "HttpOptions")) { verb = "OPTIONS"; return true; }
            }
        }

        return false;

        static bool EndsWithAttr(string name, string attr)
        {
            return name.Equals(attr, StringComparison.Ordinal) ||
                   name.EndsWith("." + attr, StringComparison.Ordinal) ||
                   name.Equals(attr + "Attribute", StringComparison.Ordinal) ||
                   name.EndsWith("." + attr + "Attribute", StringComparison.Ordinal);
        }
    }

    private static string? ExtractRouteTemplate(SyntaxList<AttributeListSyntax> lists)
    {
        // Supports:
        //   [Route("api/x")]
        //   [HttpGet("api/x")]
        //   [HttpGet, Route("api/x")]
        // We return the FIRST route-ish template we find.
        foreach (var list in lists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();

                if (EndsWithRouteLike(name))
                {
                    var arg0 = attr.ArgumentList?.Arguments.FirstOrDefault();
                    if (arg0 != null)
                        return ScanCommon.ExtractStringLiteral(arg0.Expression) ?? arg0.Expression.ToString();
                }
            }
        }

        return null;

        static bool EndsWithRouteLike(string name)
        {
            return name.Equals("Route", StringComparison.Ordinal) ||
                   name.EndsWith(".Route", StringComparison.Ordinal) ||
                   name.Equals("RouteAttribute", StringComparison.Ordinal) ||
                   name.EndsWith(".RouteAttribute", StringComparison.Ordinal) ||
                   name.EndsWith("HttpGet", StringComparison.Ordinal) ||
                   name.EndsWith("HttpGetAttribute", StringComparison.Ordinal) ||
                   name.EndsWith("HttpPost", StringComparison.Ordinal) ||
                   name.EndsWith("HttpPostAttribute", StringComparison.Ordinal) ||
                   name.EndsWith("HttpPut", StringComparison.Ordinal) ||
                   name.EndsWith("HttpPutAttribute", StringComparison.Ordinal) ||
                   name.EndsWith("HttpPatch", StringComparison.Ordinal) ||
                   name.EndsWith("HttpPatchAttribute", StringComparison.Ordinal) ||
                   name.EndsWith("HttpDelete", StringComparison.Ordinal) ||
                   name.EndsWith("HttpDeleteAttribute", StringComparison.Ordinal);
        }
    }

    private static string CombineRoutes(string? classRoute, string? methodRoute)
    {
        // Handles nulls, leading/trailing slashes, and empty templates.
        var cr = (classRoute ?? "").Trim();
        var mr = (methodRoute ?? "").Trim();

        if (string.IsNullOrWhiteSpace(cr) && string.IsNullOrWhiteSpace(mr))
            return "";

        if (string.IsNullOrWhiteSpace(cr))
            return EnsureLeadingSlash(mr);

        if (string.IsNullOrWhiteSpace(mr))
            return EnsureLeadingSlash(cr);

        // If method route looks absolute, prefer it (common pattern: [HttpGet("/api/x")])
        if (mr.StartsWith("/", StringComparison.Ordinal))
            return mr;

        return EnsureLeadingSlash($"{TrimSlashes(cr)}/{TrimSlashes(mr)}");

        static string TrimSlashes(string s) => s.Trim().Trim('/');

        static string EnsureLeadingSlash(string s)
        {
            var t = s.Trim();
            if (string.IsNullOrWhiteSpace(t)) return "";
            return t.StartsWith("/", StringComparison.Ordinal) ? t : "/" + t;
        }
    }

    private static bool IsTaskLike(string returnTypeText)
    {
        // Covers Task, Task<T>, ValueTask, ValueTask<T> as text.
        return returnTypeText.Contains("Task", StringComparison.Ordinal);
    }

    private static (bool HasListRequestParam, string? GenericArg0Type, string? GenericArg0Name) ExtractListRequestParam(MethodDeclarationSyntax method)
    {
        foreach (var p in method.ParameterList.Parameters)
        {
            var t = p.Type?.ToString();
            if (string.IsNullOrWhiteSpace(t)) continue;

            // Match:
            //   ListRequest
            //   ListRequest<T>
            //   Foo.ListRequest<T>
            //   ListRequest<Bar.Baz>
            if (t.Equals("ListRequest", StringComparison.Ordinal) ||
                t.EndsWith(".ListRequest", StringComparison.Ordinal))
            {
                return (true, null, null);
            }

            if (t.Contains("ListRequest<", StringComparison.Ordinal))
            {
                // Best-effort parse generic arg inside angle brackets
                var arg = ExtractFirstGenericArg(t);
                return (true, arg, SimpleName(arg));
            }
        }

        return (false, null, null);

        static string? ExtractFirstGenericArg(string typeText)
        {
            var lt = typeText.IndexOf('<');
            var gt = typeText.LastIndexOf('>');
            if (lt < 0 || gt <= lt) return null;

            var inner = typeText.Substring(lt + 1, gt - lt - 1).Trim();
            if (string.IsNullOrWhiteSpace(inner)) return null;

            // If multiple generic args, take the first (matches your "T is our objective")
            var comma = inner.IndexOf(',');
            if (comma > 0) inner = inner.Substring(0, comma).Trim();

            return inner;
        }

        static string? SimpleName(string? full)
        {
            if (string.IsNullOrWhiteSpace(full)) return null;
            var s = full.Trim();

            // strip global:: if present
            if (s.StartsWith("global::", StringComparison.Ordinal)) s = s.Substring("global::".Length);

            // Remove generic suffix if any
            var tick = s.IndexOf('<');
            if (tick > 0) s = s.Substring(0, tick);

            var lastDot = s.LastIndexOf('.');
            return lastDot >= 0 ? s.Substring(lastDot + 1) : s;
        }
    }
}
