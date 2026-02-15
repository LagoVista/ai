using LagoVista.AI.Chunkers.Providers.Default;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.Chunkers.Providers.Interfaces
{
    /// <summary>
    /// IDX-0042: Builds an InterfaceDescription from raw C# source text.
    ///
    /// Pure contract-level description only – no chunking/indexing concerns.
    /// </summary>
    public  class InterfaceDescriptionBuilder : DefaultDescriptionBuilder, IBuildDescriptionProcessor
    {

        public override Task<InvokeResult<IDescriptionProvider>> ProcessAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            var description = InterfaceDescriptionBuilder.CreateInterfaceDescription(ctx.Resources.FileContext, workItem.Lenses.SymbolText);

           
            
            return Task.FromResult(InvokeResult<IDescriptionProvider>.Create(description.Result));
        }

        public static InvokeResult<InterfaceDescription> CreateInterfaceDescription(IndexFileContext ctx, string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
                throw new ArgumentNullException(nameof(sourceText));

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
            var root = syntaxTree.GetCompilationUnitRoot();

            var compilation = CSharpCompilation.Create(
                "InterfaceAnalysis",
                syntaxTrees: new[] { syntaxTree },
                references: new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
                });

            var semanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true);

            var interfaceDecl = root.DescendantNodes()
                .OfType<InterfaceDeclarationSyntax>()
                .FirstOrDefault();

            if (interfaceDecl == null)
                throw new InvalidOperationException("No interface declaration was found in the provided source.");

            var symbol = semanticModel.GetDeclaredSymbol(interfaceDecl) as INamedTypeSymbol;

            var ns = GetNamespace(interfaceDecl);
            var name = interfaceDecl.Identifier.Text;
            var fullName = !string.IsNullOrWhiteSpace(ns) ? $"{ns}.{name}" : name;

            var description = new InterfaceDescription
            {
                InterfaceName = name,
                Namespace = ns,
                SourcePath = $"/{ctx.RepoId}/{ctx.RelativePath}",
                FullName = fullName,
                IsGeneric = interfaceDecl.TypeParameterList != null,
                GenericArity = interfaceDecl.TypeParameterList?.Parameters.Count ?? 0,
                BaseInterfaces = GetBaseInterfaces(symbol),
                PrimaryEntity = DetectPrimaryEntity(symbol, interfaceDecl, semanticModel),
                Role = ClassifyRole(name),
                ImplementedBy = Array.Empty<string>(),
                UsedByControllers = Array.Empty<string>(),
                LineStart = GetLine(interfaceDecl.GetLocation()?.GetLineSpan().StartLinePosition.Line),
                LineEnd = GetLine(interfaceDecl.GetLocation()?.GetLineSpan().EndLinePosition.Line)
            };

            var methods = new List<MethodDescription>();

            description.Methods = Getmethods(interfaceDecl.Members.OfType<MethodDeclarationSyntax>(), semanticModel);
            description.Properties = GetProperties(interfaceDecl.Members.OfType<PropertyDeclarationSyntax>());
            return InvokeResult<InterfaceDescription>.Create(description);
        }

        private static string GetNamespace(SyntaxNode interfaceDecl)
        {
            var parent = interfaceDecl.Parent;
            while (parent != null && !(parent is NamespaceDeclarationSyntax))
                parent = parent.Parent;

            return parent is NamespaceDeclarationSyntax ns ? ns.Name.ToString() : null;
        }

        private static string DetectPrimaryEntity(INamedTypeSymbol interfaceSymbol, InterfaceDeclarationSyntax interfaceDecl, SemanticModel model)
        {
            var name = interfaceSymbol?.Name ?? interfaceDecl.Identifier.Text;

            // 1. Naming pattern – strip I prefix and suffixes like Manager/Repository/Service
            var candidate = name;
            if (candidate.Length > 1 && candidate[0] == 'I' && char.IsUpper(candidate[1]))
            {
                candidate = candidate.Substring(1);
            }

            candidate = StripSuffix(candidate, "Manager");
            candidate = StripSuffix(candidate, "Repository");
            candidate = StripSuffix(candidate, "Service");

            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;

            // 2. Method signature heuristic – look for model-ish types in returns/parameters
            var entityCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var method in interfaceDecl.Members.OfType<MethodDeclarationSyntax>())
            {
                // Parameters
                foreach (var param in method.ParameterList.Parameters)
                {
                    var typeSymbol = model.GetTypeInfo(param.Type).Type as INamedTypeSymbol;
                    if (typeSymbol != null && LooksLikeEntityType(typeSymbol))
                        Increment(entityCounts, typeSymbol.Name);
                }

                // Return type generic arguments (e.g. Task<InvokeResult<Device>>)
                var returnTypeSymbol = model.GetTypeInfo(method.ReturnType).Type as INamedTypeSymbol;
                if (returnTypeSymbol != null)
                {
                    foreach (var arg in returnTypeSymbol.TypeArguments.OfType<INamedTypeSymbol>())
                    {
                        if (LooksLikeEntityType(arg))
                            Increment(entityCounts, arg.Name);
                    }
                }
            }

            if (entityCounts.Count > 0)
            {
                return entityCounts
                    .OrderByDescending(kvp => kvp.Value)
                    .First().Key;
            }

            return null;
        }

        private static bool LooksLikeEntityType(INamedTypeSymbol type)
        {
            if (type == null) return false;

            // Filter out obvious framework / infrastructure types
            var name = type.Name;
            if (name == "String" || name == "Int32" || name == "Boolean" || name == "Guid") return false;
            if (name == "Task" || name == "InvokeResult" || name == "ListResponse" || name == "ListRequest") return false;
            if (name == "EntityHeader") return false;

            // Prefer class-like types that are not interfaces
            return type.TypeKind == TypeKind.Class;
        }

        private static string ClassifyRole(string interfaceName)
        {
            if (string.IsNullOrWhiteSpace(interfaceName)) return null;

            if (interfaceName.EndsWith("Manager", StringComparison.Ordinal))
                return "ManagerContract";

            if (interfaceName.EndsWith("Repository", StringComparison.Ordinal))
                return "RepositoryContract";

            if (interfaceName.EndsWith("Service", StringComparison.Ordinal))
                return "ServiceContract";

            return "OtherContract";
        }

        private static string StripSuffix(string value, string suffix)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(suffix))
                return value;

            if (value.EndsWith(suffix, StringComparison.Ordinal))
                return value.Substring(0, value.Length - suffix.Length);

            return value;
        }

        private static void Increment(Dictionary<string, int> dict, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            dict[key] = dict.TryGetValue(key, out var v) ? v + 1 : 1;
        }
    }
}
