using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LagoVista.AI.Indexing.Services
{
    /// <summary>
    /// Roslyn-based implementation of IDomainMetadataSource.
    ///
    /// Responsibilities:
    /// - Scan provided C# files for [DomainDescriptor] classes.
    /// - Within those, locate the single [DomainDescription] member that
    ///   returns a DomainDescription instance.
    /// - Extract DomainKey (constant), Kind, and Description.
    /// - Associate models with domains based on ModelMetadata.DomainKey.
    /// - Record structural issues in DomainMetadata.Errors (no exceptions).
    /// </summary>
    public class RoslynDomainMetadataSource : IDomainMetadataSource
    {
        private readonly IAdminLogger _logger;

        public RoslynDomainMetadataSource(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<DomainMetadata>> GetDomainsAsync(
            IReadOnlyList<DiscoveredFile> files,
            IReadOnlyList<ModelMetadata> models,
            CancellationToken cancellationToken)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));
            if (models == null) throw new ArgumentNullException(nameof(models));

            var results = new List<DomainMetadata>();

            // Pre-index models by DomainKey for quick lookup.
            var modelsByDomain = models
                .Where(m => !string.IsNullOrWhiteSpace(m.DomainKey))
                .GroupBy(m => m.DomainKey)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            var total = files.Count;
            for (var index = 0; index < total; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var file = files[index];
                if (file.IsBinary || !file.FullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _logger.Trace($"[RoslynDomainMetadataSource_GetDomainsAsync] Processing file {index + 1}/{total}: {file.FullPath}");

                string code;
                try
                {
                    code = await File.ReadAllTextAsync(file.FullPath, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _logger.AddException("RoslynDomainMetadataSource_GetDomainsAsync", ex);
                    continue;
                }

                var tree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
                var root = tree.GetRoot(cancellationToken);

                var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                foreach (var classDecl in classDecls)
                {
                    var domainDescriptorAttrs = GetAttributesByName(classDecl.AttributeLists, "DomainDescriptor");
                    if (domainDescriptorAttrs.Count == 0)
                    {
                        continue; // Not a domain descriptor.
                    }

                    var domain = new DomainMetadata
                    {
                        RepoId = file.RepoId,
                        FullPath = file.FullPath,
                        ClassName = classDecl.Identifier.Text
                    };

                    // Look for members with [DomainDescription]. Typically a static
                    // property returning a DomainDescription instance.
                    var domainDescMembers = new List<MemberDeclarationSyntax>();
                    foreach (var member in classDecl.Members)
                    {
                        if (member is PropertyDeclarationSyntax prop && HasAttributeNamed(prop.AttributeLists, "DomainDescription"))
                        {
                            domainDescMembers.Add(member);
                        }
                        else if (member is MethodDeclarationSyntax method && HasAttributeNamed(method.AttributeLists, "DomainDescription"))
                        {
                            domainDescMembers.Add(member);
                        }
                        else if (member is FieldDeclarationSyntax field && HasAttributeNamed(field.AttributeLists, "DomainDescription"))
                        {
                            domainDescMembers.Add(member);
                        }
                    }

                    if (domainDescMembers.Count == 0)
                    {
                        domain.Errors.Add("No [DomainDescription] member found in [DomainDescriptor] class.");
                        results.Add(domain);
                        continue;
                    }

                    if (domainDescMembers.Count > 1)
                    {
                        domain.Errors.Add("Multiple [DomainDescription] members found in [DomainDescriptor] class.");
                    }

                    // Use the first [DomainDescription] member for extraction.
                    var descMember = domainDescMembers[0];

                    SyntaxList<AttributeListSyntax> attrLists =
                        (descMember as PropertyDeclarationSyntax)?.AttributeLists ??
                        (descMember as MethodDeclarationSyntax)?.AttributeLists ??
                        (descMember as FieldDeclarationSyntax)?.AttributeLists ??
                        default;

                    var descAttr = GetAttributesByName(attrLists, "DomainDescription").FirstOrDefault();

                    if (descAttr?.ArgumentList == null || descAttr.ArgumentList.Arguments.Count < 1)
                    {
                        domain.Errors.Add("[DomainDescription] must have at least 1 positional argument (domain constant).");
                    }
                    else
                    {
                        // [DomainDescription(AIAdmin)] -> DomainKey = "AIAdmin"
                        var domainArg = descAttr.ArgumentList.Arguments[0].Expression;
                        domain.DomainKey = ExtractSymbolKey(domainArg);
                    }

                    // Attempt to locate the DomainDescription initializer to read Kind/Description.
                    ExtractNameAndDescription(descMember, domain);

                    // Attach entities (if any) based on DomainKey.
                    if (!string.IsNullOrWhiteSpace(domain.DomainKey) && modelsByDomain.TryGetValue(domain.DomainKey, out var domainModels))
                    {
                        foreach (var model in domainModels)
                        {
                            domain.Entities.Add(new DomainEntitySummary
                            {
                                SymbolName = model.ClassName,
                                Title = model.Title,
                                Description = model.Description
                            });
                        }
                    }

                    results.Add(domain);
                }
            }

            return results;
        }

        private static void ExtractNameAndDescription(MemberDeclarationSyntax member, DomainMetadata domain)
        {
            // We expect a pattern like:
            // [DomainDescription(AIAdmin)]
            // public static DomainDescription AIAdminDescription
            // {
            //     get
            //     {
            //         return new DomainDescription
            //         {
            //             Kind = "AI Admin",
            //             Description = "..."
            //         };
            //     }
            // }

            ExpressionSyntax returnExpr = null;

            if (member is PropertyDeclarationSyntax prop)
            {
                if (prop.ExpressionBody != null)
                {
                    // public static DomainDescription X => new DomainDescription { ... };
                    returnExpr = prop.ExpressionBody.Expression;
                }
                else if (prop.AccessorList != null)
                {
                    var returnStmt = prop.AccessorList.Accessors
                        .Where(a => a.Body != null)
                        .SelectMany(a => a.Body.Statements.OfType<ReturnStatementSyntax>())
                        .FirstOrDefault();

                    returnExpr = returnStmt?.Expression;
                }
            }
            else if (member is MethodDeclarationSyntax method)
            {
                var returnStmt = method.Body?.Statements.OfType<ReturnStatementSyntax>().FirstOrDefault();
                returnExpr = returnStmt?.Expression;
            }

            if (returnExpr is ObjectCreationExpressionSyntax obj &&
                obj.Initializer != null &&
                obj.Type.ToString().EndsWith("DomainDescription", StringComparison.Ordinal))
            {
                foreach (var init in obj.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
                {
                    var leftName = init.Left.ToString().Trim();
                    if (init.Right is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        var value = lit.Token.ValueText;
                        if (string.Equals(leftName, "Name", StringComparison.Ordinal))
                        {
                            domain.Name = value;
                        }
                        else if (string.Equals(leftName, "Description", StringComparison.Ordinal))
                        {
                            domain.Description = value;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(domain.Name))
                {
                    domain.Errors.Add("DomainDescription initializer is missing a Name string literal.");
                }

                if (string.IsNullOrWhiteSpace(domain.Description))
                {
                    domain.Errors.Add("DomainDescription initializer is missing a Description string literal.");
                }
            }
            else
            {
                domain.Errors.Add("Unable to locate DomainDescription object initializer for Name/Description.");
            }
        }

        private static List<AttributeSyntax> GetAttributesByName(SyntaxList<AttributeListSyntax> attributeLists, string simpleName)
        {
            var list = new List<AttributeSyntax>();
            foreach (var attrList in attributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var name = attr.Name.ToString();
                    if (name.Equals(simpleName, StringComparison.Ordinal) ||
                        name.Equals(simpleName + "Attribute", StringComparison.Ordinal) ||
                        name.EndsWith("." + simpleName, StringComparison.Ordinal) ||
                        name.EndsWith("." + simpleName + "Attribute", StringComparison.Ordinal))
                    {
                        list.Add(attr);
                    }
                }
            }

            return list;
        }

        private static bool HasAttributeNamed(SyntaxList<AttributeListSyntax> attributeLists, string simpleName)
        {
            return GetAttributesByName(attributeLists, simpleName).Count > 0;
        }

        /// <summary>
        /// Extracts a symbol key used for linking models to domains. For
        /// domain constants we only care about the right-most identifier,
        /// so AIDomain.AIAdmin and AIAdmin both normalize to "AIAdmin".
        /// </summary>
        private static string ExtractSymbolKey(ExpressionSyntax expr)
        {
            switch (expr)
            {
                case IdentifierNameSyntax id:
                    return id.Identifier.Text;

                case MemberAccessExpressionSyntax member:
                    return GetRightmostIdentifier(member);

                default:
                    return expr.ToString().Trim();
            }
        }

        private static string GetRightmostIdentifier(MemberAccessExpressionSyntax member)
        {
            if (member.Name is IdentifierNameSyntax id)
            {
                return id.Identifier.Text;
            }

            if (member.Expression is MemberAccessExpressionSyntax inner)
            {
                return GetRightmostIdentifier(inner);
            }

            return member.Name.ToString();
        }
    }
}
