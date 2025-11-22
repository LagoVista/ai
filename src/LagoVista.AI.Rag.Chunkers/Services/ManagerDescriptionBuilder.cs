using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// Builds a semantic ManagerDescription (IDX-0039) from raw C# source text.
    /// This class does NOT deal with chunking or indexing concepts – only
    /// extraction of structure, intent, and relationships.
    /// </summary>
    public static class ManagerDescriptionBuilder
    {
        /// <summary>
        /// Creates a ManagerDescription from C# source text.
        /// </summary>
        public static ManagerDescription CreateManagerDescription(string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
                throw new ArgumentNullException(nameof(sourceText));

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
            var root = syntaxTree.GetCompilationUnitRoot();

            // Create a minimal compilation for semantic analysis
            var compilation = CSharpCompilation.Create(
                "ManagerAnalysis",
                syntaxTrees: new[] { syntaxTree },
                references: new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
                });

            var semanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true);

            var classDecl = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault();

            if (classDecl == null)
                throw new InvalidOperationException("No class declaration was found in the provided source.");

            var manager = new ManagerDescription
            {
                ClassName = classDecl.Identifier.Text,
                Namespace = GetNamespace(classDecl),
                Summary = GetXmlSummary(classDecl),
                ImplementedInterfaces = GetImplementedInterfaces(classDecl, semanticModel),
                Methods = new List<ManagerMethodDescription>(),
                Constructors = new List<ManagerConstructorDescription>()
            };

            // PrimaryEntity + PrimaryInterface
            manager.PrimaryEntity = DetectPrimaryEntity(classDecl, semanticModel);
            manager.PrimaryInterface = DetectPrimaryInterface(
                manager.ClassName,
                manager.PrimaryEntity,
                manager.ImplementedInterfaces);

            // Constructors
            var constructors = classDecl.Members.OfType<ConstructorDeclarationSyntax>();
            var ctorList = new List<ManagerConstructorDescription>();
            var dependencyInterfaces = new HashSet<string>(StringComparer.Ordinal);

            foreach (var ctor in constructors)
            {
                var deps = new List<string>();

                foreach (var param in ctor.ParameterList.Parameters)
                {
                    var type = semanticModel.GetTypeInfo(param.Type).Type as INamedTypeSymbol;
                    if (type != null && type.TypeKind == TypeKind.Interface)
                    {
                        deps.Add(type.Name);
                        dependencyInterfaces.Add(type.Name);
                    }
                }

                ctorList.Add(new ManagerConstructorDescription
                {
                    SignatureText = ctor.Identifier.Text + ctor.ParameterList.ToString(),
                    BodyText = ctor.Body?.ToString(),
                    LineStart = GetLineNumber(ctor.GetLocation()?.GetLineSpan().StartLinePosition.Line),
                    LineEnd = GetLineNumber(ctor.GetLocation()?.GetLineSpan().EndLinePosition.Line),
                    DependencyInterfaces = deps
                });
            }

            manager.Constructors = ctorList;
            manager.DependencyInterfaces = dependencyInterfaces.ToList();

            // Methods
            var methods = classDecl.Members.OfType<MethodDeclarationSyntax>();
            var methodList = new List<ManagerMethodDescription>();

            foreach (var method in methods)
            {
                var parameters = method.ParameterList.Parameters
                    .Select(p => new ManagerMethodParameterDescription
                    {
                        Name = p.Identifier.Text,
                        TypeName = semanticModel.GetTypeInfo(p.Type).Type?.Name ?? p.Type.ToString()
                    })
                    .ToList();

                var isPublic = method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
                var isProtectedOrInternal = method.Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword) || m.IsKind(SyntaxKind.InternalKeyword));
                var isPrivate = method.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword));

                var descriptor = new ManagerMethodDescription
                {
                    MethodName = method.Identifier.Text,
                    Summary = GetXmlSummary(method),
                    ReturnType = semanticModel.GetTypeInfo(method.ReturnType).Type?.ToDisplayString() ?? method.ReturnType.ToString(),
                    Parameters = parameters,
                    IsPublic = isPublic,
                    IsProtectedOrInternal = isProtectedOrInternal,
                    IsPrivate = isPrivate,
                    IsSignificant = IsSignificantMethod(method),
                    MethodKind = ClassifyMethodKind(method.Identifier.Text),
                    LineStart = GetLineNumber(method.GetLocation()?.GetLineSpan().StartLinePosition.Line),
                    LineEnd = GetLineNumber(method.GetLocation()?.GetLineSpan().EndLinePosition.Line),
                    BodyText = method.Body?.ToString() ?? method.ExpressionBody?.ToString()
                };

                methodList.Add(descriptor);
            }

            manager.Methods = methodList;

            return manager;
        }

        private static string GetNamespace(ClassDeclarationSyntax classDecl)
        {
            var parent = classDecl.Parent;

            while (parent != null && !(parent is NamespaceDeclarationSyntax))
                parent = parent.Parent;

            return parent is NamespaceDeclarationSyntax ns ? ns.Name.ToString() : null;
        }

        private static string GetXmlSummary(MemberDeclarationSyntax member)
        {
            var trivia = member.GetLeadingTrivia()
                .Select(t => t.GetStructure())
                .OfType<DocumentationCommentTriviaSyntax>()
                .FirstOrDefault();

            if (trivia == null) return null;

            var summary = trivia.Content
                .OfType<XmlElementSyntax>()
                .FirstOrDefault(e => e.StartTag.Name.ToString() == "summary");

            return summary?.ToString()?.Replace("<summary>", "")
                   ?.Replace("</summary>", "")?.Trim();
        }

        private static IReadOnlyList<string> GetImplementedInterfaces(
            ClassDeclarationSyntax classDecl,
            SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
            if (symbol == null) return Array.Empty<string>();

            return symbol.AllInterfaces
                .Select(i => i.Name)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static string DetectPrimaryEntity(ClassDeclarationSyntax classDecl, SemanticModel model)
        {
            var name = classDecl.Identifier.Text;
            return name.EndsWith("Manager")
                ? name.Substring(0, name.Length - "Manager".Length)
                : null;
        }

        private static string DetectPrimaryInterface(string className, string primaryEntity, IReadOnlyList<string> interfaces)
        {
            if (interfaces == null) return null;

            if (!string.IsNullOrWhiteSpace(className))
            {
                var expected = "I" + className;
                var match = interfaces.FirstOrDefault(i => i == expected);
                if (match != null) return match;
            }

            if (!string.IsNullOrWhiteSpace(primaryEntity))
            {
                var expected = "I" + primaryEntity + "Manager";
                var match = interfaces.FirstOrDefault(i => i == expected);
                if (match != null) return match;
            }

            var managerInterfaces = interfaces.Where(i => i.EndsWith("Manager")).ToList();
            return managerInterfaces.Count == 1 ? managerInterfaces[0] : null;
        }

        private static bool IsSignificantMethod(MethodDeclarationSyntax method)
        {
            if (method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))) return true;
            if (method.Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword) || m.IsKind(SyntaxKind.InternalKeyword))) return true;

            if (method.Body != null && method.Body.Statements.Count > 3)
                return true;

            return false;
        }

        private static ManagerMethodKind ClassifyMethodKind(string methodName)
        {
            if (string.IsNullOrWhiteSpace(methodName)) return ManagerMethodKind.Unknown;

            if (methodName.StartsWith("Create") || methodName.StartsWith("Add") || methodName.StartsWith("Insert"))
                return ManagerMethodKind.Create;
            if (methodName.StartsWith("Update") || methodName.StartsWith("Save") || methodName.StartsWith("Set"))
                return ManagerMethodKind.Update;
            if (methodName.StartsWith("Delete") || methodName.StartsWith("Remove"))
                return ManagerMethodKind.Delete;
            if (methodName.StartsWith("Get") || methodName.StartsWith("Find") || methodName.StartsWith("Query") || methodName.StartsWith("List"))
                return ManagerMethodKind.Query;
            if (methodName.StartsWith("Validate") || methodName.StartsWith("Ensure") || methodName.StartsWith("Check"))
                return ManagerMethodKind.Validation;

            return ManagerMethodKind.Other;
        }

        private static int? GetLineNumber(int? zeroBasedLine)
        {
            return zeroBasedLine.HasValue ? zeroBasedLine + 1 : null;
        }
    }
}
