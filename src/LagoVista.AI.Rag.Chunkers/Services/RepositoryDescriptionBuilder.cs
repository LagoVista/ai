using System;
using System.Collections.Generic;
using System.Linq;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// IDX-0040: Builds a semantic RepositoryDescription from raw C# source text.
    ///
    /// Pure description only – no chunking concerns (no PartIndex, ContentHash, etc.).
    /// Mirrors ManagerDescriptionBuilder patterns: multiple constructors, dependency
    /// interface aggregation, method shapes, primary entity heuristics, and
    /// RepositoryKind derived from the base type.
    /// </summary>
    public static class RepositoryDescriptionBuilder
    {
        public static InvokeResult<RepositoryDescription> CreateRepositoryDescription(IndexFileContext ctx, string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
                throw new ArgumentNullException(nameof(sourceText));

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
            var root = syntaxTree.GetCompilationUnitRoot();

            var compilation = CSharpCompilation.Create(
                "RepositoryAnalysis",
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
            var baseTypeName = baseType?.Name;

            var description = new RepositoryDescription
            {
                ClassName = classDecl.Identifier.Text,
                Namespace = GetNamespace(classDecl),
                Summary = GetXmlSummary(classDecl),
                ImplementedInterfaces = GetImplementedInterfaces(classDecl, semanticModel),
                BaseTypeName = baseTypeName,
                RepositoryKind = DetectRepositoryKind(baseType),
                Methods = new List<RepositoryMethodDescription>(),
                Constructors = new List<RepositoryConstructorDescription>(),
                DependencyInterfaces = Array.Empty<string>(),
                StorageProfile = null
            };

            description.SetCommonProperties(ctx);

            // PrimaryEntity detection (IDX-0040 heuristics)
            description.PrimaryEntity = DetectPrimaryEntity(classDecl, semanticModel, baseType);

            // Constructors + dependency interfaces (union across all ctors)
            PopulateConstructorsAndDependencies(classDecl, semanticModel, description);

            // Methods
            var methodList = new List<RepositoryMethodDescription>();

            foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>())
            {
                var parameters = method.ParameterList.Parameters
                    .Select(p => new RepositoryMethodParameterDescription
                    {
                        Name = p.Identifier.Text,
                        TypeName = semanticModel.GetTypeInfo(p.Type).Type?.Name ?? p.Type.ToString()
                    })
                    .ToArray();

                var descriptor = new RepositoryMethodDescription
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

                methodList.Add(descriptor);
            }

            description.Methods = methodList;

            // Optional: future StorageProfile inference hook – left null for now
            // description.StorageProfile = DetectStorageProfile(baseType, description.PrimaryEntity);

            return InvokeResult<RepositoryDescription>.Create(description);
        }

        private static void PopulateConstructorsAndDependencies(
            ClassDeclarationSyntax classDecl,
            SemanticModel semanticModel,
            RepositoryDescription description)
        {
            var ctorSyntaxes = classDecl.Members
                .OfType<ConstructorDeclarationSyntax>()
                .ToList();

            if (ctorSyntaxes.Count == 0)
                return;

            var ctorDescriptions = new List<RepositoryConstructorDescription>();
            var dependencies = new HashSet<string>(StringComparer.Ordinal);

            foreach (var ctor in ctorSyntaxes)
            {
                var ctorParams = ctor.ParameterList.Parameters
                    .Select(p => new RepositoryMethodParameterDescription
                    {
                        Name = p.Identifier.Text,
                        TypeName = semanticModel.GetTypeInfo(p.Type).Type?.Name ?? p.Type.ToString()
                    })
                    .ToArray();

                var ctorDesc = new RepositoryConstructorDescription
                {
                    Parameters = ctorParams,
                    LineStart = GetLine(ctor.GetLocation()?.GetLineSpan().StartLinePosition.Line),
                    LineEnd = GetLine(ctor.GetLocation()?.GetLineSpan().EndLinePosition.Line),
                    BodyText = ctor.Body?.ToString() ?? ctor.ExpressionBody?.ToString()
                };

                ctorDescriptions.Add(ctorDesc);

                foreach (var param in ctor.ParameterList.Parameters)
                {
                    var typeSymbol = semanticModel.GetTypeInfo(param.Type).Type as INamedTypeSymbol;
                    if (typeSymbol?.TypeKind == TypeKind.Interface)
                        dependencies.Add(typeSymbol.Name);
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

        private static RepositoryKind DetectRepositoryKind(INamedTypeSymbol baseType)
        {
            if (baseType == null)
                return RepositoryKind.Unknown;

            var name = baseType.Name;
            var full = baseType.ToDisplayString();

            // Tune these mappings as you add more concrete base types.
            if (name.Equals("DocumentDBRepoBase", StringComparison.Ordinal)
                || full.Contains("DocumentDBRepoBase", StringComparison.Ordinal))
                return RepositoryKind.DocumentDb;

            if (name.Contains("TableStorage", StringComparison.Ordinal)
                || full.Contains("TableStorageRepoBase", StringComparison.Ordinal))
                return RepositoryKind.TableStorage;

            if (name.Contains("Sql", StringComparison.Ordinal)
                || full.Contains("SqlRepoBase", StringComparison.Ordinal))
                return RepositoryKind.Sql;

            if (name.Contains("InMemory", StringComparison.Ordinal)
                || full.Contains("InMemoryRepo", StringComparison.Ordinal))
                return RepositoryKind.InMemory;

            return RepositoryKind.Other;
        }

        private static string DetectPrimaryEntity(
            ClassDeclarationSyntax classDecl,
            SemanticModel model,
            INamedTypeSymbol baseType)
        {
            var className = classDecl.Identifier.Text;

            // 1. Base class generic argument (strongest)
            if (baseType != null && baseType.IsGenericType && baseType.TypeArguments.Length > 0)
            {
                // e.g., DocumentDBRepoBase<AgentContext> -> AgentContext
                var entityArg = baseType.TypeArguments[0] as INamedTypeSymbol;
                if (entityArg != null)
                    return entityArg.Name;
            }

            // 2. Class name pattern: <EntityName>Repository or <EntityName>Repo
            if (className.EndsWith("Repository", StringComparison.Ordinal))
                return className.Substring(0, className.Length - "Repository".Length);

            if (className.EndsWith("Repo", StringComparison.Ordinal))
                return className.Substring(0, className.Length - "Repo".Length);

            // 3. Add/Insert/Upsert/Save first parameter heuristic
            var candidateMethods = classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Identifier.Text.StartsWith("Add", StringComparison.Ordinal)
                         || m.Identifier.Text.StartsWith("Insert", StringComparison.Ordinal)
                         || m.Identifier.Text.StartsWith("Upsert", StringComparison.Ordinal)
                         || m.Identifier.Text.StartsWith("Save", StringComparison.Ordinal));

            foreach (var method in candidateMethods)
            {
                var firstParam = method.ParameterList.Parameters.FirstOrDefault();
                if (firstParam?.Type == null) continue;

                var type = model.GetTypeInfo(firstParam.Type).Type as INamedTypeSymbol;
                if (type != null)
                    return type.Name;
            }

            // 4. Method signature dominance (parameter and return types)
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

        private static RepositoryMethodKind ClassifyMethodKind(string methodName)
        {
            if (string.IsNullOrWhiteSpace(methodName)) return RepositoryMethodKind.Unknown;

            // Query-like methods first
            if (methodName.StartsWith("Query", StringComparison.OrdinalIgnoreCase)
                || methodName.StartsWith("List", StringComparison.OrdinalIgnoreCase))
            {
                return RepositoryMethodKind.Query;
            }

            // "Get" methods that clearly look like list/query operations
            if (methodName.StartsWith("Get", StringComparison.OrdinalIgnoreCase))
            {
                if (methodName.IndexOf("Summaries", StringComparison.OrdinalIgnoreCase) >= 0
                    || methodName.IndexOf("List", StringComparison.OrdinalIgnoreCase) >= 0
                    || methodName.IndexOf("ForOrg", StringComparison.OrdinalIgnoreCase) >= 0
                    || methodName.IndexOf("ForOrganisation", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return RepositoryMethodKind.Query;
                }

                // Fallback: treat remaining Get* methods as GetById-style
                return RepositoryMethodKind.GetById;
            }

            if (methodName.StartsWith("Add", StringComparison.OrdinalIgnoreCase)
                || methodName.StartsWith("Insert", StringComparison.OrdinalIgnoreCase)
                || methodName.StartsWith("Save", StringComparison.OrdinalIgnoreCase))
                return RepositoryMethodKind.Insert;

            if (methodName.StartsWith("Update", StringComparison.OrdinalIgnoreCase)
                || methodName.StartsWith("Upsert", StringComparison.OrdinalIgnoreCase))
                return RepositoryMethodKind.Update;

            if (methodName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)
                || methodName.StartsWith("Remove", StringComparison.OrdinalIgnoreCase))
                return RepositoryMethodKind.Delete;

            return RepositoryMethodKind.Other;
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
