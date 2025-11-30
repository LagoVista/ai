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

            // Locate field that holds the DomainDescription for this domain.
            var field = root
                .DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .FirstOrDefault(f => f.Declaration?.Variables
                    .Any(v => string.Equals(v.Identifier.Text, domain.DomainKey, StringComparison.Ordinal)) == true);

            if (field == null)
            {
                throw new InvalidOperationException(
                    $"Could not find a field named '{domain.DomainKey}' in '{domain.FullPath}'.");
            }

            var variable = field.Declaration.Variables
                .First(v => string.Equals(v.Identifier.Text, domain.DomainKey, StringComparison.Ordinal));

            var objectCreation = variable.Initializer?.Value as ObjectCreationExpressionSyntax;
            if (objectCreation == null)
            {
                throw new InvalidOperationException(
                    $"Field '{domain.DomainKey}' in '{domain.FullPath}' does not have a DomainDescription initializer.");
            }

            var initializer = objectCreation.Initializer;
            if (initializer == null)
            {
                throw new InvalidOperationException(
                    $"DomainDescription initializer for '{domain.DomainKey}' in '{domain.FullPath}' has no object initializer.");
            }

            var updatedInitializer = UpdateProperty(initializer, "Name", review.RefinedTitle);
            updatedInitializer = UpdateProperty(updatedInitializer, "Description", review.RefinedDescription);

            if (updatedInitializer == initializer)
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
