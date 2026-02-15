using LagoVista.AI;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.Chunkers.Providers.Managers
{
    /// <summary>
    /// IDX-0039: Builds a semantic ManagerDescription from raw C# source text.
    ///
    /// Pure description only – no chunking/indexing concerns here. This is the
    /// upstream description used later for chunk builders and RAG payloads.
    /// Mirrors RepositoryDescriptionBuilder patterns: multiple ctors, aggregated
    /// dependency interfaces, method shapes, and PrimaryEntity heuristics.
    /// </summary>
    public class ManagerDescriptionBuilder : IBuildDescriptionProcessor
    {
        public Task<InvokeResult<IDescriptionProvider>> ProcessAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            return Task.FromResult(InvokeResult<IDescriptionProvider>.Create(null));
        }

        public static InvokeResult<ManagerDescription> CreateManagerDescription(IndexFileContext ctx, string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
                throw new ArgumentNullException(nameof(sourceText));

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
            var root = syntaxTree.GetCompilationUnitRoot();

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

            var symbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
            var baseType = symbol?.BaseType as INamedTypeSymbol;

            var description = new ManagerDescription
            {
                ClassName = classDecl.Identifier.Text,
                Namespace = GetNamespace(classDecl),
                Summary = GetXmlSummary(classDecl),
                BaseTypeName = baseType?.Name,
                ImplementedInterfaces = GetImplementedInterfaces(classDecl, semanticModel),
                Methods = new List<ManagerMethodDescription>(),
                Constructors = new List<ManagerConstructorDescription>(),
                DependencyInterfaces = Array.Empty<string>()
            };

            description.SetCommonProperties(ctx);

            // PrimaryEntity detection (IDX-0039 heuristics)
            description.PrimaryEntity = DetectPrimaryEntity(classDecl, semanticModel);

            // Constructors + dependency interfaces (union across all ctors)
            PopulateConstructorsAndDependencies(classDecl, semanticModel, description);

            // Methods
            var methods = new List<ManagerMethodDescription>();

            foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>())
            {
                var parameters = method.ParameterList.Parameters
                    .Select(p => new ManagerMethodParameterDescription
                    {
                        Name = p.Identifier.Text,
                        TypeName = semanticModel.GetTypeInfo(p.Type).Type?.Name ?? p.Type.ToString()
                    })
                    .ToArray();

                var desc = new ManagerMethodDescription
                {
                    MethodName = method.Identifier.Text,
                    Summary = GetXmlSummary(method),
                    ReturnType = semanticModel.GetTypeInfo(method.ReturnType).Type?.ToDisplayString() ?? method.ReturnType.ToString(),
                    Parameters = parameters,
                    IsPublic = method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)),
                    IsProtectedOrInternal = method.Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword) || m.IsKind(SyntaxKind.InternalKeyword)),
                    IsPrivate = method.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)),
                    IsSignificant = IsSignificantMethod(method),
                    MethodKind = ClassifyMethodKind(method.Identifier.Text),
                    LineStart = GetLine(method.GetLocation()?.GetLineSpan().StartLinePosition.Line),
                    LineEnd = GetLine(method.GetLocation()?.GetLineSpan().EndLinePosition.Line),
                    BodyText = method.Body?.ToString() ?? method.ExpressionBody?.ToString()
                };

                methods.Add(desc);
            }

            description.Methods = methods;

            return InvokeResult<ManagerDescription>.Create(description);
        }

        private static void PopulateConstructorsAndDependencies(
            ClassDeclarationSyntax classDecl,
            SemanticModel semanticModel,
            ManagerDescription description)
        {
            var ctorSyntaxes = classDecl.Members
                .OfType<ConstructorDeclarationSyntax>()
                .ToList();

            if (ctorSyntaxes.Count == 0)
                return;

            var ctorDescriptions = new List<ManagerConstructorDescription>();
            var dependencies = new HashSet<string>(StringComparer.Ordinal);

            foreach (var ctor in ctorSyntaxes)
            {
                var ctorParams = ctor.ParameterList.Parameters
                    .Select(p => new ManagerMethodParameterDescription
                    {
                        Name = p.Identifier.Text,
                        TypeName = semanticModel.GetTypeInfo(p.Type).Type?.Name ?? p.Type.ToString()
                    })
                    .ToArray();

                var ctorDesc = new ManagerConstructorDescription
                {
                    Parameters = ctorParams,
                    LineStart = GetLine(ctor.GetLocation()?.GetLineSpan().StartLinePosition.Line),
                    LineEnd = GetLine(ctor.GetLocation()?.GetLineSpan().EndLinePosition.Line),
                    BodyText = ctor.Body?.ToString() ?? ctor.ExpressionBody?.ToString()
                };

                ctorDescriptions.Add(ctorDesc);

                // Aggregate dependency interfaces across ALL constructors
                foreach (var param in ctor.ParameterList.Parameters)
                {
                    var typeInfo = semanticModel.GetTypeInfo(param.Type);
                    var typeSymbol = typeInfo.Type as INamedTypeSymbol;

                    // If Roslyn can resolve it as an interface, great.
                    if (typeSymbol?.TypeKind == TypeKind.Interface)
                    {
                        dependencies.Add(typeSymbol.Name);
                        continue;
                    }

                    // Fallback for error types / unresolved symbols – use naming convention.
                    var typeName = param.Type.ToString();
                    var simpleName = StripGenericSuffix(typeName);

                    if (LooksLikeInterfaceName(simpleName))
                    {
                        // SimpleName may include namespace; strip it to match tests.
                        var shortName = simpleName.Split('.').Last();
                        dependencies.Add(shortName);
                    }
                }
            }

            description.Constructors = ctorDescriptions;
            description.DependencyInterfaces = dependencies.ToList();
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

            return summary?.ToString()
                .Replace("<summary>", string.Empty)
                .Replace("</summary>", string.Empty)
                .Trim();
        }

        private static IReadOnlyList<string> GetImplementedInterfaces(ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
            return symbol == null
                ? Array.Empty<string>()
                : symbol.AllInterfaces.Select(i => i.Name).Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string DetectPrimaryEntity(ClassDeclarationSyntax classDecl, SemanticModel model)
        {
            var className = classDecl.Identifier.Text;

            // 1. Class Kind Pattern (Strongest): <EntityName>Manager
            if (className.EndsWith("Manager", StringComparison.Ordinal))
            {
                var entityName = className.Substring(0, className.Length - "Manager".Length);
                if (!string.IsNullOrWhiteSpace(entityName))
                    return entityName;
            }

            // 2. Add/Create first parameter heuristic
            var candidateMethods = classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Identifier.Text.StartsWith("Add", StringComparison.Ordinal)
                         || m.Identifier.Text.StartsWith("Create", StringComparison.Ordinal));

            foreach (var method in candidateMethods)
            {
                var firstParam = method.ParameterList.Parameters.FirstOrDefault();
                if (firstParam?.Type == null) continue;

                var type = model.GetTypeInfo(firstParam.Type).Type as INamedTypeSymbol;
                if (type != null)
                    return type.Name;
            }

            // 3. Method signature dominance (parameters + return types)
            var typeCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>())
            {
                var rt = model.GetTypeInfo(method.ReturnType).Type as INamedTypeSymbol;
                if (rt != null)
                    Increment(typeCounts, rt.Name);

                foreach (var p in method.ParameterList.Parameters)
                {
                    var pt = model.GetTypeInfo(p.Type).Type as INamedTypeSymbol;
                    if (pt != null)
                        Increment(typeCounts, pt.Name);
                }
            }

            if (typeCounts.Count > 0)
            {
                return typeCounts
                    .OrderByDescending(kvp => kvp.Value)
                    .First().Key;
            }

            return null;
        }

        private static bool IsSignificantMethod(MethodDeclarationSyntax method)
        {
            if (method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))) return true;
            if (method.Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword) || m.IsKind(SyntaxKind.InternalKeyword))) return true;
            return method.Body != null && method.Body.Statements.Count > 1;
        }

        private static ManagerMethodKind ClassifyMethodKind(string methodName)
        {
            if (string.IsNullOrWhiteSpace(methodName)) return ManagerMethodKind.Unknown;

            if (methodName.StartsWith("Add", StringComparison.OrdinalIgnoreCase)
                || methodName.StartsWith("Create", StringComparison.OrdinalIgnoreCase))
                return ManagerMethodKind.Create;

            if (methodName.StartsWith("Update", StringComparison.OrdinalIgnoreCase)
                || methodName.StartsWith("Upsert", StringComparison.OrdinalIgnoreCase))
                return ManagerMethodKind.Update;

            if (methodName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)
                || methodName.StartsWith("Remove", StringComparison.OrdinalIgnoreCase))
                return ManagerMethodKind.Delete;

            if (methodName.StartsWith("Validate", StringComparison.OrdinalIgnoreCase)
                || methodName.IndexOf("Validation", StringComparison.OrdinalIgnoreCase) >= 0)
                return ManagerMethodKind.Validation;

            if (methodName.StartsWith("Get", StringComparison.OrdinalIgnoreCase)
                || methodName.StartsWith("List", StringComparison.OrdinalIgnoreCase)
                || methodName.StartsWith("Query", StringComparison.OrdinalIgnoreCase))
                return ManagerMethodKind.Query;

            return ManagerMethodKind.Other;
        }

        private static string StripGenericSuffix(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return typeName;
            var idx = typeName.IndexOf('<');
            return idx >= 0 ? typeName.Substring(0, idx) : typeName;
        }

        private static bool LooksLikeInterfaceName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var simple = name.Split('.').Last();
            return simple.Length > 1 && simple[0] == 'I' && char.IsUpper(simple[1]);
        }

        private static void Increment(Dictionary<string, int> dict, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            dict[key] = dict.TryGetValue(key, out var v) ? v + 1 : 1;
        }

        private static int? GetLine(int? zeroBased)
        {
            return zeroBased.HasValue ? zeroBased + 1 : null;
        }
    }
}
