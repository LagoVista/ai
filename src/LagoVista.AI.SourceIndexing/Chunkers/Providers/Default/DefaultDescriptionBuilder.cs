using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.Chunkers.Providers.Default
{
    public class DefaultDescriptionBuilder : IBuildDescriptionProcessor
    {
        public virtual Task<InvokeResult<IDescriptionProvider>> ProcessAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            var descriptionProvider = new DefaultDescription(); ;

            return Task.FromResult(InvokeResult<IDescriptionProvider>.Create(descriptionProvider));
        }

        protected static IReadOnlyList<string> GetBaseInterfaces(INamedTypeSymbol symbol)
        {
            if (symbol == null || symbol.AllInterfaces.Length == 0)
                return Array.Empty<string>();

            return symbol.AllInterfaces
                .Select(i => i.ToDisplayString())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        protected static string GetXmlSummary(MemberDeclarationSyntax member)
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

        protected static string GetParameterTypeName(ParameterSyntax parameter, SemanticModel model)
        {
            if (parameter.Type == null)
                return null;

            var typeSymbol = model.GetTypeInfo(parameter.Type).Type as INamedTypeSymbol;
            if (typeSymbol != null)
            {
                return typeSymbol.Name;
            }

            // Fallback to syntactic type name
            return parameter.Type.ToString();
        }

        protected static bool IsAsyncReturnType(TypeSyntax returnType)
        {
            if (returnType == null) return false;

            var text = returnType.ToString();
            if (string.IsNullOrWhiteSpace(text)) return false;

            // Simple heuristic: Task or Task<T>
            if (text.StartsWith("Task<", StringComparison.Ordinal)
                || text.Equals("Task", StringComparison.Ordinal)
                || text.StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        public static List<MethodDescription> Getmethods(IEnumerable<MethodDeclarationSyntax> methodDeclarations, SemanticModel semanticModel)
        {
            var methods = new List<MethodDescription>();

            foreach (var method in methodDeclarations)
            {
                var parameters = method.ParameterList.Parameters
                    .Select(p => new MethodParameterDescription
                    {
                        Name = p.Identifier.Text,
                        Type = GetParameterTypeName(p, semanticModel),
                        IsOptional = p.Default != null,
                        DefaultValue = p.Default?.Value?.ToString()
                    })
                    .ToArray();

                var returnTypeString = method.ReturnType.ToString();

                var methodDesc = new MethodDescription
                {
                    Name = method.Identifier.Text,
                    ReturnType = returnTypeString,
                    IsAsync = IsAsyncReturnType(method.ReturnType),
                    Parameters = parameters,
                    Summary = GetXmlSummary(method),
                    LineStart = GetLine(method.GetLocation()?.GetLineSpan().StartLinePosition.Line),
                    LineEnd = GetLine(method.GetLocation()?.GetLineSpan().EndLinePosition.Line)
                };

                methods.Add(methodDesc);
            }
            return methods;
        }

        protected static List<PropertyDescription> GetProperties(IEnumerable<PropertyDeclarationSyntax> propertyDeclarations)
        {
            var properties = new List<PropertyDescription>();
            foreach (var property in propertyDeclarations)
            {
                var propertyDescription = new PropertyDescription()
                {
                    Name = property.Identifier.Text,
                    Type = property.Type.ToString(),
                    HasGetter = property.AccessorList?.Accessors.Any(a => a.Kind() == SyntaxKind.GetAccessorDeclaration) == true,
                    HasSetter = property.AccessorList?.Accessors.Any(a => a.Kind() == SyntaxKind.SetAccessorDeclaration) == true,
                    LineStart = GetLine(property.GetLocation()?.GetLineSpan().StartLinePosition.Line),
                    LineEnd = GetLine(property.GetLocation()?.GetLineSpan().EndLinePosition.Line)
                };

                properties.Add(propertyDescription);
            }

            return properties;
        }


        protected static int? GetLine(int? zeroBased)
        {
            return zeroBased.HasValue ? zeroBased + 1 : null;
        }
    }
}
