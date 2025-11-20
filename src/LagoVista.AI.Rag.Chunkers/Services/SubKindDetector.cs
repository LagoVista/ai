using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// Detects SubKind for server-side C# code using syntax-only heuristics
    /// derived from IDX-024 and IDX-031 plus an additional Test SubKind.
    /// Works purely from source text and relative path – no reflection required.
    /// </summary>
    public enum CodeSubKind
    {
        DomainDescription,
        Model,
        Manager,
        Repository,
        Controller,
        Service,
        Test,
        Interface,
        Startup,
        Program,
        Client,
        Configuration,
        Handler,
        ResourceFile,
        CodeAttribute,
        Exception,
        ExtensionMethods,
        Request,
        Result,
        Response,
        ProxyServices,
        Other
    }

    public sealed class SubKindDetectionResult
    {
        public string Path { get; set; }
        public CodeSubKind SubKind { get; set; }
        public string SubKindString => SubKind.ToString();
        public string PrimaryTypeName { get; set; }
        public bool IsMixed { get; set; }
        public string Reason { get; set; }
        public IReadOnlyList<string> Evidence { get; set; } = Array.Empty<string>();
    }

    public static class SubKindDetector
    {
        private sealed class TypeKindInference
        {
            public string Name { get; set; }
            public CodeSubKind? Kind { get; set; }
            public List<string> Evidence { get; } = new List<string>();
        }

        /// <summary>
        /// Detects the most appropriate SubKind for a C# source file.
        /// </summary>
        public static SubKindDetectionResult DetectForFile(string sourceText, string relativePath)
        {
            if (sourceText == null) throw new ArgumentNullException(nameof(sourceText));

            var tree = CSharpSyntaxTree.ParseText(sourceText);
            var root = tree.GetRoot();

            if(relativePath.ToLower().EndsWith("resx"))
            {
                return new SubKindDetectionResult
                {
                    SubKind = CodeSubKind.ResourceFile,
                    PrimaryTypeName = null,
                    IsMixed = false,
                    Path = relativePath,
                    Reason = "File name ended with RESX, is a ersource file.",
                    Evidence = Array.Empty<string>()
                };
            }

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
                return new SubKindDetectionResult
                {
                    SubKind = CodeSubKind.Other,
                    PrimaryTypeName = null,
                    IsMixed = false,
                    Path = relativePath,
                    Reason = "No top-level types found; defaulting SubKind=Other.",
                    Evidence = Array.Empty<string>()
                };
            }

            var inferences = new List<TypeKindInference>();

            foreach (var type in typeNodes)
            {
                var info = new TypeKindInference
                {
                    Name = type.Identifier.ValueText
                };

                var ns = GetNamespace(type);

                // Evaluate in priority order for this type.
                // Tests first so that test projects referencing domain/models/etc are classified as Test.
                if (IsTest(type, ns, segments, info.Evidence))
                {
                    info.Kind = CodeSubKind.Test;
                }
                else if (IsDomainDescription(type, ns, segments, info.Evidence))
                {
                    info.Kind = CodeSubKind.DomainDescription;
                }
                else if (IsException(type, ns, segments, info.Evidence))
                {
                    info.Kind = CodeSubKind.Exception;
                }
                else if (IsModel(type, ns, segments, info.Evidence))
                {
                    info.Kind = CodeSubKind.Model;
                }
                else if (IsManager(type, ns, segments, info.Evidence))
                {
                    info.Kind = CodeSubKind.Manager;
                }
                else if (IsRepository(type, ns, segments, info.Evidence))
                {
                    info.Kind = CodeSubKind.Repository;
                }
                else if (IsController(type, ns, segments, info.Evidence))
                {
                    info.Kind = CodeSubKind.Controller;
                }
                else if (IsService(type, ns, segments, info.Evidence))
                {
                    info.Kind = CodeSubKind.Service;
                }
                else if (IsInterfaceType(type, ns, segments, info.Evidence))
                {
                    info.Kind = CodeSubKind.Interface;
                }
                else if(IsStartup(type, ns, segments, info.Evidence))
                {
                    info.Kind = CodeSubKind.Startup;
                }
                

                inferences.Add(info);
            }

            var distinctKinds = inferences
                .Select(i => i.Kind)
                .Where(k => k.HasValue)
                .Select(k => k.Value)
                .Distinct()
                .ToList();

            CodeSubKind chosen;

            // File-level priority: Tests win over everything else.
            if (distinctKinds.Contains(CodeSubKind.Test))
                chosen = CodeSubKind.Test;
            else if (distinctKinds.Contains(CodeSubKind.DomainDescription))
                chosen = CodeSubKind.DomainDescription;
            else if (distinctKinds.Contains(CodeSubKind.Model))
                chosen = CodeSubKind.Model;
            else if (distinctKinds.Contains(CodeSubKind.Manager))
                chosen = CodeSubKind.Manager;
            else if (distinctKinds.Contains(CodeSubKind.Repository))
                chosen = CodeSubKind.Repository;
            else if (distinctKinds.Contains(CodeSubKind.Controller))
                chosen = CodeSubKind.Controller;
            else if (distinctKinds.Contains(CodeSubKind.Service))
                chosen = CodeSubKind.Service;
            else if (distinctKinds.Contains(CodeSubKind.Interface))
                chosen = CodeSubKind.Interface;
            else if (distinctKinds.Contains(CodeSubKind.Exception))
                chosen = CodeSubKind.Exception;
            else if (distinctKinds.Contains(CodeSubKind.Client))
                chosen = CodeSubKind.Client;
            else if (distinctKinds.Contains(CodeSubKind.CodeAttribute))
                chosen = CodeSubKind.CodeAttribute;
            else
                chosen = CodeSubKind.Other;

            var isMixed = distinctKinds.Count > 1;

            var winningTypes = inferences
                .Where(i => i.Kind == chosen)
                .ToList();

            var primaryTypeName = winningTypes.FirstOrDefault()?.Name ?? inferences.First().Name;

            var evidence = winningTypes
                .SelectMany(i => i.Evidence)
                .Distinct()
                .ToArray();

            if(chosen == CodeSubKind.Other)
            {
                if(FallbackSubKindDetector(primaryTypeName, segments, out CodeSubKind subKind))
                {
                    chosen = subKind;
                }
            }

            var reason = evidence.Length == 0
                ? $"SubKind={chosen} inferred by fallback rules."
                : $"SubKind={chosen} inferred based on: {string.Join("; ", evidence)}";

            return new SubKindDetectionResult
            {
                SubKind = chosen,
                PrimaryTypeName = primaryTypeName,
                IsMixed = isMixed,
                Reason = reason,
                Evidence = evidence,
                Path = relativePath
            };
        }

        private static bool FallbackSubKindDetector(String typeName, string[] segments, out CodeSubKind subKind)
        {
            if(ClassEndsWith(typeName, "repo", "repository"))
            {
                subKind = CodeSubKind.Repository;
                return true;
            }

            if (ClassEndsWith(typeName, "handler"))
            {
                subKind = CodeSubKind.Handler;
                return true;
            }

            if (ClassEndsWith(typeName, "client"))
            {
                subKind = CodeSubKind.Client;
                return true;
            }

            if (ClassEndsWith(typeName, "config"))
            {
                subKind = CodeSubKind.Configuration;
                return true;
            }

            if (ClassEndsWith(typeName, "extensions"))
            {
                subKind = CodeSubKind.ExtensionMethods;
                return true;
            }

            if (typeName.ToLower() == "program")
            {
                subKind = CodeSubKind.Program;
                return true;
            }

            if (ClassEndsWith(typeName, "service"))
            {
                subKind = CodeSubKind.Service;
                return true;
            }

            if (ClassEndsWith(typeName, "attribute"))
            {
                subKind = CodeSubKind.CodeAttribute;
                return true;
            }

            if (typeName.ToLower() == "startup")
            {
                subKind = CodeSubKind.Startup;
                return true;
            }

            if (ClassEndsWith(typeName, "request"))
            {
                subKind = CodeSubKind.Request;
                return true;
            }

            if (ClassEndsWith(typeName, "result"))
            {
                subKind = CodeSubKind.Result;
                return true;
            }

            if (ClassEndsWith(typeName, "response"))
            {
                subKind = CodeSubKind.Response;
                return true;
            }

            if (ClassEndsWith(typeName, "proxy"))
            {
                subKind = CodeSubKind.ProxyServices;
                return true;
            }

            if (segments.Any(seg => seg.ToLower().Contains("proxy")))
            {
                subKind = CodeSubKind.ProxyServices;
                return true;
            }


            if (segments.Any(seg => seg.ToLower() == "extensions"))
            {
                subKind = CodeSubKind.ExtensionMethods;
                return true;
            }

            subKind = CodeSubKind.Other;

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
            if (type.GetType().Name == "Startup")
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

        private static bool IsTest(
            TypeDeclarationSyntax type,
            string ns,
            string[] segments,
            List<string> evidence)
        {
            // We treat anything that clearly looks like a test type as Test, regardless of what it tests.

            // Common attributes (NUnit, MSTest, xUnit-style wrappers, etc.)
            if (HasAttribute(type, "TestFixture", "TestClass"))
            {
                evidence.Add($"Type {type.Identifier.ValueText} has a known test fixture attribute.");
                return true;
            }

            // Naming convention: *Tests or *Test
            var name = type.Identifier.ValueText;
            if (name.EndsWith("Tests", StringComparison.Ordinal) || name.EndsWith("Test", StringComparison.Ordinal))
            {
                evidence.Add($"Type {name} name ends with 'Test' or 'Tests'.");
                return true;
            }

            // Namespace convention: *.Tests.*
            if (!string.IsNullOrEmpty(ns) && ns.IndexOf(".Tests", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                evidence.Add($"Namespace '{ns}' contains '.Tests'.");
                return true;
            }

            // Path convention: /tests/ or /test/
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
                foreach (var attr in list.Attributes)
                {
                    var rawName = attr.Name.ToString(); // may include namespace
                    var simple = GetSimpleIdentifier(rawName);
                    if (names.Contains(simple) || names.Contains(simple.Replace("Attribute", string.Empty)))
                        return true;
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
            typeName = typeName.ToLower(); 

            foreach (var s in suffix)
                if (typeName.EndsWith(s.ToLower()))
                    return true;

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

            return segments.Any(s =>
                s.Equals(value, StringComparison.OrdinalIgnoreCase));
        }

        private static bool LooksLikeInterfaceName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 2) return false;
            return name[0] == 'I' && char.IsUpper(name[1]);
        }

        private static string GetSimpleIdentifier(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return typeName;

            // Strip generic arity
            var angleIndex = typeName.IndexOf('<');
            if (angleIndex >= 0)
                typeName = typeName.Substring(0, angleIndex);

            // Strip namespace
            var dotIndex = typeName.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex < typeName.Length - 1)
                typeName = typeName.Substring(dotIndex + 1);

            return typeName.Trim();
        }
    }
}
