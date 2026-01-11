using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LagoVista.AI.Rag.Chunkers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// Single-symbol SubKind analyzer.
    ///
    /// This is the canonical home for all SubKind heuristics that used to live
    /// in SubKindDetector. It is intended to be called on isolated symbol text
    /// (e.g., output of SymbolSplitter). If more than one top-level type is
    /// detected, an InvalidOperationException is thrown.
    /// </summary>
    public static class SourceKindAnalyzer
    {
        /// <summary>
        /// Analyze an isolated C# symbol and return a single SourceKindResult.
        ///
        /// If more than one top-level type is detected, this indicates a pipeline
        /// bug (callers must split the file first) and an InvalidOperationException
        /// is thrown.
        /// </summary>
        public static SourceKindResult AnalyzeFile(string sourceText, string relativePath)
        {
            if (sourceText == null) throw new ArgumentNullException(nameof(sourceText));

            // Special case: .resx files are treated as ResourceFile without Roslyn parsing.
            if (!string.IsNullOrEmpty(relativePath) &&
                relativePath.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
            {
                return new SourceKindResult
                {
                    Path = relativePath,
                    SubKind = SubtypeKind.ResourceFile,
                    PrimaryTypeName = null,
                    IsMixed = false,
                    Reason = "File name ended with .resx, classified as ResourceFile.",
                    Evidence = Array.Empty<string>(),
                    SymbolText = null
                };
            }

            var tree = CSharpSyntaxTree.ParseText(sourceText);
            var root = (CompilationUnitSyntax)tree.GetRoot();

            var segments = (relativePath ?? string.Empty)
                .Replace('\\', '/')
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            var typeNodes = root
                .DescendantNodes()
                .Where(n =>
                    n is ClassDeclarationSyntax ||
                    n is StructDeclarationSyntax ||
                    n is RecordDeclarationSyntax ||
                    n is InterfaceDeclarationSyntax)
                .OfType<TypeDeclarationSyntax>()
                .ToList();

            if (typeNodes.Count == 0)
            {
                return new SourceKindResult
                {
                    Path = relativePath,
                    SubKind = SubtypeKind.Other,
                    PrimaryTypeName = null,
                    IsMixed = false,
                    Reason = "No top-level types found; defaulting SubKind=Other.",
                    Evidence = Array.Empty<string>(),
                    SymbolText = null
                };
            }

            if (typeNodes.Count > 1)
            {
                var fileLabel = string.IsNullOrWhiteSpace(relativePath) ? "<unknown>" : relativePath;

                throw new InvalidOperationException(
                    $"SourceKindAnalyzer.AnalyzeFile expected a single top-level type but found {typeNodes.Count} in '{fileLabel}'.  Found {String.Join(',', typeNodes.Select(tn=> tn.Identifier))} " +
                    "This API must only be called with isolated symbol text (e.g., SymbolSplitter output)."
                );
            }

            var type = typeNodes[0];
            var evidence = new List<string>();
            var ns = GetNamespace(type);

            // Primary classification (priority order).
            SubtypeKind kind;

            if (IsTest(type, ns, segments, evidence))
            {
                kind = SubtypeKind.Test;
            }
            else if(IsDdr(type, ns, segments, evidence))
            {
                kind = SubtypeKind.Ddr;
            }
            else if (IsMarkDown(type, ns, segments, evidence))
            {
                kind = SubtypeKind.MarkDown;
            }
            else if (IsDomainDescription(type, ns, segments, evidence))
            {
                kind = SubtypeKind.DomainDescription;
            }
            else if (IsException(type, ns, segments, evidence))
            {
                kind = SubtypeKind.Exception;
            }
            else if (IsListModel(type, ns, segments, evidence))
            {
                kind = SubtypeKind.SummaryListModel;
            }
            else if (IsModel(type, ns, segments, evidence))
            {
                kind = SubtypeKind.Model;
            }
            else if (IsManager(type, ns, segments, evidence))
            {
                kind = SubtypeKind.Manager;
            }
            else if (IsRepository(type, ns, segments, evidence))
            {
                kind = SubtypeKind.Repository;
            }
            else if (IsController(type, ns, segments, evidence))
            {
                kind = SubtypeKind.Controller;
            }
            else if (IsService(type, ns, segments, evidence))
            {
                kind = SubtypeKind.Service;
            }
            else if (IsInterfaceType(type, ns, segments, evidence))
            {
                kind = SubtypeKind.Interface;
            }
            else if (IsStartup(type, ns, segments, evidence))
            {
                kind = SubtypeKind.Startup;
            }
            else
            {
                kind = SubtypeKind.Other;
            }

            // Fallback name/path-based detection if still Other.
            if (kind == SubtypeKind.Other)
            {
                if (FallbackSubKindDetector(type.Identifier.ValueText, segments, out var fallbackKind) &&
                    fallbackKind != SubtypeKind.Other)
                {
                    kind = fallbackKind;
                    evidence.Add($"FallbackSubKindDetector matched '{type.Identifier.ValueText}'/path pattern -> {fallbackKind}.");
                }
            }

            var evidenceArray = evidence.Distinct().ToArray();
            var reason = evidenceArray.Length == 0
                ? $"SubKind={kind} inferred by fallback rules."
                : $"SubKind={kind} inferred based on: {string.Join("; ", evidenceArray)}";

            return new SourceKindResult
            {
                Path = relativePath,
                SubKind = kind,
                PrimaryTypeName = type.Identifier.ValueText,
                IsMixed = false,
                Reason = reason,
                Evidence = evidenceArray,
                SymbolText = BuildIsolatedSymbolText(type, root)
            };
        }

        private static bool FallbackSubKindDetector(string typeName, string[] segments, out SubtypeKind subKind)
        {
            var fileName = segments.Length > 0 ? segments[^1] : string.Empty;

            if (ClassEndsWith(typeName, "repo", "repository"))
            {
                subKind = SubtypeKind.Repository;
                return true;
            }

            if (ClassEndsWith(typeName, "handler"))
            {
                subKind = SubtypeKind.Handler;
                return true;
            }

            if (ClassEndsWith(typeName, "client"))
            {
                subKind = SubtypeKind.Client;
                return true;
            }

            if (ClassEndsWith(typeName, "config"))
            {
                subKind = SubtypeKind.Configuration;
                return true;
            }

            if (ClassEndsWith(typeName, "extensions"))
            {
                subKind = SubtypeKind.ExtensionMethods;
                return true;
            }

            if (typeName.Equals("program", StringComparison.OrdinalIgnoreCase))
            {
                subKind = SubtypeKind.Program;
                return true;
            }

            if (ClassEndsWith(typeName, "service") || ClassEndsWith(typeName, "services"))
            {
                subKind = SubtypeKind.Service;
                return true;
            }

            if (ClassEndsWith(typeName, "attribute"))
            {
                subKind = SubtypeKind.CodeAttribute;
                return true;
            }

            if (typeName.Equals("startup", StringComparison.OrdinalIgnoreCase))
            {
                subKind = SubtypeKind.Startup;
                return true;
            }

            if (ClassEndsWith(typeName, "request"))
            {
                subKind = SubtypeKind.Request;
                return true;
            }

            if (ClassEndsWith(typeName, "result"))
            {
                subKind = SubtypeKind.Result;
                return true;
            }

            if (ClassEndsWith(typeName, "response"))
            {
                subKind = SubtypeKind.Response;
                return true;
            }

            if (ClassEndsWith(typeName, "message"))
            {
                subKind = SubtypeKind.Message;
                return true;
            }

            if (ClassEndsWith(typeName, "proxy"))
            {
                subKind = SubtypeKind.ProxyServices;
                return true;
            }

            var fileLower = fileName.ToLowerInvariant();
            if (fileLower.EndsWith("result") || fileLower.EndsWith("results"))
            {
                subKind = SubtypeKind.Result;
                return true;
            }

            if (segments.Any(seg => seg.ToLowerInvariant().Contains("proxy")))
            {
                subKind = SubtypeKind.ProxyServices;
                return true;
            }

            if (segments.Any(seg => seg.Equals("extensions", StringComparison.OrdinalIgnoreCase)))
            {
                subKind = SubtypeKind.ExtensionMethods;
                return true;
            }

            subKind = SubtypeKind.Other;
            return false;
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

        private static bool IsDomainDescription(
            TypeDeclarationSyntax type,
            string ns,
            string[] segments,
            List<string> evidence)
        {
            if (HasAttribute(type, "DomainDescriptor", "DomainDescription"))
            {
                evidence.Add($"Type {type.Identifier.ValueText} has DomainDescriptor/DomainDescription attribute.");
                return true;
            }

            if (!string.IsNullOrEmpty(ns) &&
                (ns.IndexOf(".Domain", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 ns.IndexOf(".Descriptors", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                evidence.Add($"Namespace '{ns}' contains '.Domain' or '.Descriptors'.");
                return true;
            }

            if (HasPathSegment(segments, "domain") || HasPathSegment(segments, "descriptors"))
            {
                evidence.Add("Path contains 'domain' or 'descriptors' segment.");
                return true;
            }

            return false;
        }

        private static bool IsListModel(
            TypeDeclarationSyntax type,
            string ns,
            string[] segments,
            List<string> evidence)
        {
            if (InheritsBase(type, "SummaryData"))
            {
                evidence.Add($"Type {type.Identifier.ValueText} inherits from SummaryData.");
                return true;
            }

            return false;
        }

        private static bool IsModel(
            TypeDeclarationSyntax type,
            string ns,
            string[] segments,
            List<string> evidence)
        {
            if (HasAttribute(type, "EntityDescription"))
            {
                evidence.Add($"Type {type.Identifier.ValueText} has EntityDescription attribute.");
                return true;
            }

            if (InheritsBase(type, "EntityBase"))
            {
                evidence.Add($"Type {type.Identifier.ValueText} inherits from EntityBase.");
                return true;
            }

            if (!string.IsNullOrEmpty(ns) &&
                ns.IndexOf(".Models", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                evidence.Add($"Namespace '{ns}' contains '.Models'.");
                return true;
            }

            if (HasPathSegment(segments, "models"))
            {
                evidence.Add("Path contains 'models' segment.");
                return true;
            }

            return false;
        }

        private static bool IsException(
            TypeDeclarationSyntax type,
            string ns,
            string[] segments,
            List<string> evidence)
        {
            if (InheritsBase(type, "Exception"))
            {
                evidence.Add($"Type {type.Identifier.ValueText} inherits from Exception.");
                return true;
            }

            if (!string.IsNullOrEmpty(ns) &&
                ns.IndexOf(".Exceptions", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                evidence.Add($"Namespace '{ns}' contains '.Exceptions'.");
                return true;
            }

            if (HasPathSegment(segments, "exceptions"))
            {
                evidence.Add("Path contains 'exceptions' segment.");
                return true;
            }

            return false;
        }

        private static bool IsManager(
            TypeDeclarationSyntax type,
            string ns,
            string[] segments,
            List<string> evidence)
        {
            // Interfaces should not be classified as Manager even if the path/namespace matches
            if (type is InterfaceDeclarationSyntax)
                return false;

            if (ImplementsInterfacePattern(type, "Manager"))
            {
                evidence.Add($"Type {type.Identifier.ValueText} implements I*Manager interface.");
                return true;
            }

            if (InheritsBase(type, "ManagerBase"))
            {
                evidence.Add($"Type {type.Identifier.ValueText} inherits from ManagerBase.");
                return true;
            }

            if (!string.IsNullOrEmpty(ns) &&
                ns.IndexOf(".Managers", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                evidence.Add($"Namespace '{ns}' contains '.Managers'.");
                return true;
            }

            if (HasPathSegment(segments, "managers"))
            {
                evidence.Add("Path contains 'managers' segment.");
                return true;
            }

            return false;
        }

        private static bool IsRepository(
            TypeDeclarationSyntax type,
            string ns,
            string[] segments,
            List<string> evidence)
        {
            // Interfaces should not be classified as Repository even if the path/namespace matches
            if (type is InterfaceDeclarationSyntax)
                return false;

            // Base class detection – include both *RepoBase and TableStorageBase variants
            if (InheritsBase(type, "DocumentDBRepoBase", "TableStorageRepoBase", "TableStorageBase", "CloudFileStorage"))
            {
                evidence.Add($"Type {type.Identifier.ValueText} inherits from a repository base type.");
                return true;
            }

            // Interface pattern I*Repository
            if (ImplementsInterfacePattern(type, "Repository"))
            {
                evidence.Add($"Type {type.Identifier.ValueText} implements I*Repository interface.");
                return true;
            }

            // Namespace hint: *.Repositories* or *.Repo*
            if (!string.IsNullOrEmpty(ns) &&
                (ns.IndexOf(".Repositories", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 ns.IndexOf(".Repo", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                evidence.Add($"Namespace '{ns}' contains '.Repositories' or '.Repo'.");
                return true;
            }

            // Path hint: /repositories/, /repos/, or /repo/
            if (HasPathSegment(segments, "repositories") ||
                HasPathSegment(segments, "repos") ||
                HasPathSegment(segments, "repo"))
            {
                evidence.Add("Path contains 'repositories', 'repos', or 'repo' segment.");
                return true;
            }

            return false;
        }

        private static bool IsController(
            TypeDeclarationSyntax type,
            string ns,
            string[] segments,
            List<string> evidence)
        {
            // Interfaces should not be classified as Controller even if the path/namespace matches
            if (type is InterfaceDeclarationSyntax)
                return false;

            if (HasAttribute(type, "ApiController"))
            {
                evidence.Add($"Type {type.Identifier.ValueText} has ApiController attribute.");
                return true;
            }

            if (InheritsBase(type, "LagoVistaBaseController", "ControllerBase", "Controller"))
            {
                evidence.Add($"Type {type.Identifier.ValueText} inherits from a controller base type.");
                return true;
            }

            if (!string.IsNullOrEmpty(ns) &&
                ns.IndexOf(".Controllers", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                evidence.Add($"Namespace '{ns}' contains '.Controllers'.");
                return true;
            }

            if (HasPathSegment(segments, "controllers"))
            {
                evidence.Add("Path contains 'controllers' segment.");
                return true;
            }

            return false;
        }

        private static bool IsStartup(
            TypeDeclarationSyntax type,
            string ns,
            string[] segments,
            List<string> evidence)
        {
            if (string.Equals(type.Identifier.ValueText, "Startup", StringComparison.Ordinal))
            {
                evidence.Add($"Type {type.Identifier.ValueText} is named 'Startup'.");
                return true;
            }

            return false;
        }

        private static bool IsService(
            TypeDeclarationSyntax type,
            string ns,
            string[] segments,
            List<string> evidence)
        {
            // Interfaces should not be classified as Service even if the path/namespace matches
            if (type is InterfaceDeclarationSyntax)
                return false;

            if (ImplementsInterfacePattern(type, "Service"))
            {
                evidence.Add($"Type {type.Identifier.ValueText} implements I*Service interface.");
                return true;
            }

            if (!string.IsNullOrEmpty(ns) &&
                ns.IndexOf(".Services", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                evidence.Add($"Namespace '{ns}' contains '.Services'.");
                return true;
            }

            if (HasPathSegment(segments, "services"))
            {
                evidence.Add("Path contains 'services' segment.");
                return true;
            }

            return false;
        }

        private static bool IsDdr(TypeDeclarationSyntax type, string ns, string[] segmments, List<string> evidence)
        {
            if (segmments[0].ToLower() == "ddrs")
            {
                evidence.Add($"In root DDRs folder");
                return true;
            }
            return false;
        }

        private static bool IsMarkDown(TypeDeclarationSyntax type, string ns, string[] segmments, List<string> evidence)
        {
            if (segmments.Last().ToLower().EndsWith("md"))
            {
                evidence.Add($"File ends with markdown");
                return true;
            }
            return false;
        }

        private static bool IsTest(
            TypeDeclarationSyntax type,
            string ns,
            string[] segments,
            List<string> evidence)
        {
            // Common attributes (NUnit, MSTest, etc.)
            if (HasAttribute(type, "TestFixture", "TestClass"))
            {
                evidence.Add($"Type {type.Identifier.ValueText} has a known test fixture attribute.");
                return true;
            }

            var name = type.Identifier.ValueText;
            if (name.EndsWith("Tests", StringComparison.Ordinal) || name.EndsWith("Test", StringComparison.Ordinal))
            {
                evidence.Add($"Type {name} name ends with 'Test' or 'Tests'.");
                return true;
            }

            if (!string.IsNullOrEmpty(ns) &&
                ns.IndexOf(".Tests", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                evidence.Add($"Namespace '{ns}' contains '.Tests'.");
                return true;
            }

            if (HasPathSegment(segments, "tests") || HasPathSegment(segments, "test"))
            {
                evidence.Add("Path contains 'tests' or 'test' segment.");
                return true;
            }

            return false;
        }

        private static bool IsInterfaceType(
            TypeDeclarationSyntax type,
            string ns,
            string[] segments,
            List<string> evidence)
        {
            if (type is InterfaceDeclarationSyntax && LooksLikeInterfaceName(type.Identifier.ValueText))
            {
                evidence.Add($"Type {type.Identifier.ValueText} is an interface with name starting with 'I'.");
                return true;
            }

            return false;
        }

        private static bool HasAttribute(TypeDeclarationSyntax type, params string[] attributeNames)
        {
            var names = new HashSet<string>(attributeNames, StringComparer.OrdinalIgnoreCase);

            foreach (var list in type.AttributeLists)
            {
                foreach (var attr in list.Attributes)
                {
                    var rawName = attr.Name.ToString();
                    var simple = GetSimpleIdentifier(rawName);
                    if (names.Contains(simple) || names.Contains(simple.Replace("Attribute", string.Empty)))
                        return true;
                }
            }

            return false;
        }

        private static bool InheritsBase(TypeDeclarationSyntax type, params string[] baseTypeNames)
        {
            if (type.BaseList == null) return false;

            var names = new HashSet<string>(baseTypeNames, StringComparer.OrdinalIgnoreCase);

            foreach (var bt in type.BaseList.Types)
            {
                var simple = GetSimpleIdentifier(bt.Type.ToString());
                if (names.Contains(simple))
                    return true;
            }

            return false;
        }

        private static bool ClassEndsWith(string typeName, params string[] suffix)
        {
            var lower = typeName.ToLowerInvariant();

            foreach (var s in suffix)
            {
                if (lower.EndsWith(s.ToLowerInvariant()))
                    return true;
            }

            return false;
        }

        private static bool ImplementsInterfacePattern(TypeDeclarationSyntax type, string suffix)
        {
            if (type.BaseList == null) return false;

            foreach (var bt in type.BaseList.Types)
            {
                var simple = GetSimpleIdentifier(bt.Type.ToString());
                if (simple.Length > suffix.Length + 1 &&
                    simple.StartsWith("I", StringComparison.Ordinal) &&
                    simple.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPathSegment(string[] segments, string value)
        {
            if (segments == null || segments.Length == 0) return false;

            return segments.Any(s => s.Equals(value, StringComparison.OrdinalIgnoreCase));
        }

        private static bool LooksLikeInterfaceName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 2) return false;
            return name[0] == 'I' && char.IsUpper(name[1]);
        }

        private static string GetSimpleIdentifier(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return typeName;

            var angleIndex = typeName.IndexOf('<');
            if (angleIndex >= 0)
                typeName = typeName.Substring(0, angleIndex);

            var dotIndex = typeName.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex < typeName.Length - 1)
                typeName = typeName.Substring(dotIndex + 1);

            return typeName.Trim();
        }

        /// <summary>
        /// Builds a self-contained C# snippet for a single type (including usings and namespace).
        /// </summary>
        private static string BuildIsolatedSymbolText(TypeDeclarationSyntax type, CompilationUnitSyntax root)
        {
            var sb = new StringBuilder();

            var usingNodes = new List<UsingDirectiveSyntax>();
            usingNodes.AddRange(root.Usings);

            var nsNode = type
                .Ancestors()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .FirstOrDefault();

            if (nsNode != null)
            {
                usingNodes.AddRange(nsNode.Usings);
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var u in usingNodes)
            {
                var key = u.ToString().Trim();
                if (seen.Add(key))
                {
                    sb.Append(u.ToFullString());
                }
            }

            var ns = GetNamespace(type);

            if (!string.IsNullOrEmpty(ns))
            {
                if (sb.Length > 0 && !sb.ToString().EndsWith(Environment.NewLine, StringComparison.Ordinal))
                {
                    sb.AppendLine();
                }

                sb.Append("namespace ").Append(ns).AppendLine();
                sb.AppendLine("{");

                var typeText = type.ToFullString();
                sb.Append(IndentWith(typeText, "    "));
                sb.AppendLine();
                sb.AppendLine("}");
            }
            else
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.Append(type.ToFullString());
            }

            return sb.ToString();
        }

        private static string IndentWith(string text, string indent)
        {
            if (text == null) return string.Empty;

            var normalized = text.Replace("\r\n", "\n");
            var lines = normalized.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length > 0)
                {
                    lines[i] = indent + lines[i];
                }
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
