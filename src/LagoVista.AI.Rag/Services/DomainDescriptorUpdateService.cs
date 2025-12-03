using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Roslyn-based implementation of <see cref="IDomainDescriptorUpdateService"/>.
    /// Safely rewrites DomainDescription initializers for domain descriptors.
    ///
    /// Primary pattern (preferred):
    ///   [DomainDescription(SomeDomainKey)]
    ///   public static readonly DomainDescription SomeDomainDescription = new DomainDescription { ... };
    ///
    /// Fallback patterns supported for legacy code:
    ///   [DomainDescription(SomeDomainKey)]
    ///   public static DomainDescription SomeDomainDescription => new DomainDescription { ... };
    ///
    ///   [DomainDescription(SomeDomainKey)]
    ///   public static DomainDescription SomeDomainDescription
    ///   {
    ///       get { return new DomainDescription { ... }; }
    ///   }
    ///
    /// Any other shapes will log a failure and be skipped for update.
    /// </summary>
    public class DomainDescriptorUpdateService : IDomainDescriptorUpdateService
    {
        private readonly IAdminLogger _logger;

        public DomainDescriptorUpdateService(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task UpdateAsync(DomainMetadata domain, TitleDescriptionReviewResult review, CancellationToken cancellationToken)
        {
            if (domain == null) throw new ArgumentNullException(nameof(domain));
            if (review == null) throw new ArgumentNullException(nameof(review));

            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(domain.FullPath))
            {
                throw new InvalidOperationException("Domain metadata does not contain a valid FullPath.");
            }

            if (!File.Exists(domain.FullPath))
            {
                throw new FileNotFoundException($"Domain descriptor file not found: {domain.FullPath}", domain.FullPath);
            }

            var sourceText = File.ReadAllText(domain.FullPath);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
            var root = syntaxTree.GetRoot() as CompilationUnitSyntax;
            if (root == null)
            {
                throw new InvalidOperationException($"Unable to parse domain descriptor file: {domain.FullPath}");
            }

            // Locate member (field or property) that holds the DomainDescription for this domain.
            var member = FindDomainDescriptionMember(root);
            if (member == null)
            {
                throw new InvalidOperationException(
                    $"Could not find a static field or property with [DomainDescription] of type DomainDescription in '{domain.FullPath}'.");
            }

            if (!TryGetDomainDescriptionObjectCreation(member, out var objectCreation))
            {
                throw new InvalidOperationException(
                    $"Domain descriptor member in '{domain.FullPath}' does not have a DomainDescription object initializer.");
            }

            if (objectCreation.Initializer == null)
            {
                throw new InvalidOperationException(
                    $"DomainDescription initializer in '{domain.FullPath}' has no object initializer.");
            }

            var updatedInitializer = UpdateProperty(objectCreation.Initializer, "Name", review.RefinedTitle);
            updatedInitializer = UpdateProperty(updatedInitializer, "Description", review.RefinedDescription);

            if (updatedInitializer == objectCreation.Initializer)
            {
                // Nothing changed; no need to rewrite.
                return Task.CompletedTask;
            }

            var updatedObjectCreation = objectCreation.WithInitializer(updatedInitializer);
            var updatedRoot = root.ReplaceNode(objectCreation, updatedObjectCreation);
            var updatedSource = updatedRoot.NormalizeWhitespace().ToFullString();

            File.WriteAllText(domain.FullPath, updatedSource);

            _logger.Trace(
                $"[DomainDescriptorUpdateService] Updated domain '{domain.DomainKey}' in '{domain.FullPath}'.");

            return Task.CompletedTask;
        }

        private static MemberDeclarationSyntax FindDomainDescriptionMember(CompilationUnitSyntax root)
        {
            return root
                .DescendantNodes()
                .OfType<MemberDeclarationSyntax>()
                .FirstOrDefault(m => HasDomainDescriptionAttribute(m) && IsDomainDescriptionMember(m));
        }

        private static bool HasDomainDescriptionAttribute(MemberDeclarationSyntax member)
        {
            if (member == null)
            {
                return false;
            }

            return member.AttributeLists
                .SelectMany(l => l.Attributes)
                .Any(a =>
                {
                    var name = a.Name.ToString();
                    return string.Equals(name, "DomainDescription", StringComparison.Ordinal) ||
                           name.EndsWith(".DomainDescription", StringComparison.Ordinal);
                });
        }

        private static bool IsDomainDescriptionMember(MemberDeclarationSyntax member)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    return field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) &&
                           IsDomainDescriptionType(field.Declaration?.Type);

                case PropertyDeclarationSyntax property:
                    return property.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) &&
                           IsDomainDescriptionType(property.Type);

                default:
                    return false;
            }
        }

        private static bool IsDomainDescriptionType(TypeSyntax type)
        {
            if (type == null)
            {
                return false;
            }

            var typeName = type.ToString();
            return string.Equals(typeName, "DomainDescription", StringComparison.Ordinal) ||
                   typeName.EndsWith(".DomainDescription", StringComparison.Ordinal);
        }

        private static bool TryGetDomainDescriptionObjectCreation(
            MemberDeclarationSyntax member,
            out ObjectCreationExpressionSyntax objectCreation)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    return TryGetDomainDescriptionObjectCreation(field, out objectCreation);

                case PropertyDeclarationSyntax property:
                    return TryGetDomainDescriptionObjectCreation(property, out objectCreation);

                default:
                    objectCreation = null;
                    return false;
            }
        }

        private static bool TryGetDomainDescriptionObjectCreation(
            FieldDeclarationSyntax field,
            out ObjectCreationExpressionSyntax objectCreation)
        {
            objectCreation = null;

            if (field?.Declaration?.Variables == null || field.Declaration.Variables.Count == 0)
            {
                return false;
            }

            var variableWithInitializer = field.Declaration.Variables
                .FirstOrDefault(v => v.Initializer != null);

            if (variableWithInitializer == null)
            {
                return false;
            }

            objectCreation = variableWithInitializer.Initializer.Value as ObjectCreationExpressionSyntax;

            return objectCreation != null && IsDomainDescriptionType(objectCreation.Type);
        }

        private static bool TryGetDomainDescriptionObjectCreation(
            PropertyDeclarationSyntax property,
            out ObjectCreationExpressionSyntax objectCreation)
        {
            objectCreation = null;

            if (property == null)
            {
                return false;
            }

            // Expression-bodied property:
            //   public static DomainDescription X => new DomainDescription { ... };
            if (property.ExpressionBody?.Expression is ObjectCreationExpressionSyntax exprBodyCreation &&
                IsDomainDescriptionType(exprBodyCreation.Type))
            {
                objectCreation = exprBodyCreation;
                return true;
            }

            if (property.AccessorList == null)
            {
                return false;
            }

            var getter = property.AccessorList.Accessors
                .FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));

            if (getter == null)
            {
                return false;
            }

            // Expression-bodied getter:
            //   get => new DomainDescription { ... };
            if (getter.ExpressionBody?.Expression is ObjectCreationExpressionSyntax getterExprBodyCreation &&
                IsDomainDescriptionType(getterExprBodyCreation.Type))
            {
                objectCreation = getterExprBodyCreation;
                return true;
            }

            // Block-bodied getter with a single return statement:
            //   get { return new DomainDescription { ... }; }
            if (getter.Body != null)
            {
                var returnStatement = getter.Body.Statements
                    .OfType<ReturnStatementSyntax>()
                    .SingleOrDefault();

                if (returnStatement?.Expression is ObjectCreationExpressionSyntax returnCreation &&
                    IsDomainDescriptionType(returnCreation.Type))
                {
                    objectCreation = returnCreation;
                    return true;
                }
            }

            return false;
        }

        private static InitializerExpressionSyntax UpdateProperty(
            InitializerExpressionSyntax initializer,
            string propertyName,
            string newValue)
        {
            if (initializer == null)
            {
                return initializer;
            }

            var assignment = initializer.Expressions
                .OfType<AssignmentExpressionSyntax>()
                .FirstOrDefault(a =>
                    a.Left is IdentifierNameSyntax ident &&
                    string.Equals(ident.Identifier.Text, propertyName, StringComparison.Ordinal));

            if (assignment == null)
            {
                return initializer;
            }

            var literal = SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(newValue ?? string.Empty));

            var updatedAssignment = assignment.WithRight(literal);

            return initializer.ReplaceNode(assignment, updatedAssignment);
        }
    }
}
