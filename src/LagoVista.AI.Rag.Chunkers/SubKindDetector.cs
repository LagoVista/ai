using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LagoVista.AI.Rag.Chunkers
{
    /// <summary>
    /// Detects SubKind for server-side C# code using syntax-only heuristics
    /// derived from IDX-024 and IDX-031.
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
        Interface,
        Other
    }

    public sealed class SubKindDetectionResult
    {
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
                if (IsDomainDescription(type, ns, segments, info.Evidence))
                {
                    info.Kind = CodeSubKind.DomainDescription;
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

                inferences.Add(info);
            }

            var distinctKinds = inferences
                .Select(i => i.Kind)
                .Where(k => k.HasValue)
                .Select(k => k.Value)
                .Distinct()
                .ToList();

            CodeSubKind chosen;

            if (distinctKinds.Contains(CodeSubKind.DomainDescription))
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

            var reason = evidence.Length == 0
                ? $"SubKind={chosen} inferred by fallback rules."
                : $"SubKind={chosen} inferred based on: {string.Join("; ", evidence)}";

            return new SubKindDetectionResult
            {
                SubKind = chosen,
                PrimaryTypeName = primaryTypeName,
                IsMixed = isMixed,
                Reason = reason,
                Evidence = evidence
            };
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

        private static bool IsManager(
            TypeDeclarationSyntax type,
            string ns,
            string[] segments,
            List<string> evidence)
        {
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
            if (InheritsBase(type, "DocumentDBRepoBase", "TableStorageRepoBase"))
            {
                evidence.Add($"Type {type.Identifier.ValueText} inherits from a repository base type.");
                return true;
            }

            if (ImplementsInterfacePattern(type, "Repository"))
            {
                evidence.Add($"Type {type.Identifier.ValueText} implements I*Repository interface.");
                return true;
            }

            if (!string.IsNullOrEmpty(ns) &&
                ns.IndexOf(".Repositories", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                evidence.Add($"Namespace '{ns}' contains '.Repositories'.");
                return true;
            }

            if (HasPathSegment(segments, "repositories") || HasPathSegment(segments, "repos"))
            {
                evidence.Add("Path contains 'repositories' or 'repos' segment.");
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

        private static bool IsService(
            TypeDeclarationSyntax type,
            string ns,
            string[] segments,
            List<string> evidence)
        {
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
