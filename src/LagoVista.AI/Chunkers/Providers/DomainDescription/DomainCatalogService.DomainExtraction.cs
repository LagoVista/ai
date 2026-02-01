using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Chunkers.Providers.DomainDescription;
using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Models.UIMetaData;                // DomainDescription
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Partial class containing the guts for domain extraction logic.
    /// Roslyn + CSharpSymbolSplitter pipeline is kept here so the primary service
    /// file stays stable.
    /// </summary>
    public sealed partial class DomainCatalogService
    {
        /// <summary>
        /// Extracts unique domain summaries from the provided C# files.
        ///
        /// Rules:
        /// - Only .cs files are considered.
        /// - Any file under tests/... is ignored.
        /// - Uses CSharpSymbolSplitter to get one-class snippets.
        /// - Each snippet is parsed with Roslyn to find [DomainDescriptor]
        ///   classes and their DomainSummaryInfo.
        /// - Enforces mandatory fields (DomainKey, Title, Description) at this level.
        /// </summary>
        private async Task<IReadOnlyList<DomainSummaryInfo>> ExtractDomainsAsync(
            IReadOnlyList<DiscoveredFile> files,
            CancellationToken cancellationToken)
        {
            var domainsByKey = new Dictionary<string, DomainSummaryInfo>(StringComparer.OrdinalIgnoreCase);

            _adminLogger.Trace($"[DomainCatalogService__ExtractDomainsAsync] - scanning {files.Count} files for [DomainDescriptor] classes.");

            for (var idx = 0; idx < files.Count; idx++)
            {
                var file = files[idx];

                if (idx % 100 == 0)
                {
                    _adminLogger.Trace(
                        $"[DomainCatalogService__ExtractDomainsAsync] - scanned {idx} of {files.Count} files - found {domainsByKey.Count} domains, {(idx * 100.0 / Math.Max(1, files.Count)):0.0}% complete.");
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Only consider C# files.
                if (!file.FullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Exclude tests root: tests/... should never contribute to the catalog.
                var relative = (file.RelativePath ?? string.Empty).Replace('\\', '/');
                if (relative.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) ||
                    relative.Equals("tests", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!File.Exists(file.FullPath))
                {
                    throw new FileNotFoundException(
                        $"Discovered file does not exist on disk: '{file.FullPath}'.",
                        file.FullPath);
                }

                var source = await File.ReadAllTextAsync(file.FullPath, cancellationToken).ConfigureAwait(false);

                // Fast pre-check: only pay CSharpSymbolSplitter/Roslyn cost if the file
                // even mentions [DomainDescriptor].
                if (source.IndexOf("[DomainDescriptor", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                //var splitterResults = CSharpSymbolSplitter.Chunk(source);
                //if (!splitterResults.Successful)
                //{
                //    throw new InvalidOperationException(
                //        $"SymbolSplitter failed for file '{file.RelativePath ?? file.FullPath}'.");
                //}

                //// CSharpSymbolSplitter guarantees each snippet contains only one class.
                //foreach (var snippet in splitterResults.Result)
                //{
                //    cancellationToken.ThrowIfCancellationRequested();

                //    var text = snippet.Text;
                //    if (string.IsNullOrWhiteSpace(text))
                //    {
                //        continue;
                //    }

                //    var summaries = ExtractDomainsFromSnippet(text);
                //    if (summaries == null || summaries.Count == 0)
                //    {
                //        continue;
                //    }

                //    foreach (var summary in summaries)
                //    {
                //        if (string.IsNullOrWhiteSpace(summary.DomainKey) ||
                //            string.IsNullOrWhiteSpace(summary.Title) ||
                //            string.IsNullOrWhiteSpace(summary.Description))
                //        {
                //            throw new InvalidOperationException(
                //                $"Domain descriptor in '{file.RelativePath ?? file.FullPath}' produced an incomplete DomainSummaryInfo. DomainKey, Title, and Description are all required.");
                //        }

                //        if (!domainsByKey.ContainsKey(summary.DomainKey))
                //        {
                //            domainsByKey.Add(summary.DomainKey, summary);
                //        }
                //        // If the key already exists, we keep the first definition. If
                //        // you want stricter duplicate detection, we can add that later
                //        // without changing this method signature.
                //    }
                //}
            }

            return domainsByKey.Values.ToList();
        }

        /// <summary>
        /// Core Roslyn-based extractor for a single-class snippet.
        ///
        /// This is effectively the inlined logic from the former
        /// DomainDescriptorSummaryExtractor, but scoped to a single snippet that
        /// contains exactly one class declaration.
        ///
        /// It is kept private and exercised via tests using reflection to avoid
        /// expanding the public surface.
        /// </summary>
        private static IReadOnlyList<DomainSummaryInfo> ExtractDomainsFromSnippet(string source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();

            var summaries = new List<DomainSummaryInfo>();

            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!HasAttribute(classDecl.AttributeLists, "DomainDescriptor"))
                    continue;

                ExtractFromDomainClass(classDecl, summaries);
            }

            return summaries;
        }

        private static void ExtractFromDomainClass(
            ClassDeclarationSyntax classDecl,
            List<DomainSummaryInfo> target)
        {
            var typeName = classDecl.Identifier.Text;
            var fullTypeName = typeName;

            if (classDecl.Parent is NamespaceDeclarationSyntax ns)
            {
                fullTypeName = ns.Name + "." + typeName;
            }

            var constStringFields = BuildConstStringLookup(classDecl);

            foreach (var prop in classDecl.Members.OfType<PropertyDeclarationSyntax>())
            {
                if (!IsStaticPublic(prop))
                    continue;

                if (!IsDomainDescriptionType(prop.Type))
                    continue;

                var domainAttr = GetAttribute(prop.AttributeLists, "DomainDescription");
                if (domainAttr == null)
                    continue;

                var domainKey = ResolveDomainKey(domainAttr, constStringFields) ?? string.Empty;

                var (title, description, domainTypeName) = ExtractDomainDescriptionInitializer(prop);

                var domainType = DomainDescription.DomainTypes.BusinessObject;
                if (!string.IsNullOrWhiteSpace(domainTypeName) &&
                    Enum.TryParse(domainTypeName, ignoreCase: true, out DomainDescription.DomainTypes parsed))
                {
                    domainType = parsed;
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    title = !string.IsNullOrWhiteSpace(domainKey) ? domainKey : prop.Identifier.Text;
                }

                var info = new DomainSummaryInfo(
                    domainKey: !string.IsNullOrWhiteSpace(domainKey) ? domainKey : title,
                    domainKeyName: ResolveDomainKeyName(domainAttr, constStringFields),
                    title: title,
                    description: description ?? string.Empty,
                    domainType: domainType,
                    sourceTypeName: fullTypeName,
                    sourcePropertyName: prop.Identifier.Text);

                target.Add(info);
            }
        }

        private static (string title, string description, string domainTypeName) ExtractDomainDescriptionInitializer(PropertyDeclarationSyntax prop)
        {
            var getter = prop.AccessorList?.Accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.GetAccessorDeclaration);
            if (getter == null)
                return (null, null, null);

            var returnStmt = getter.Body?.Statements.OfType<ReturnStatementSyntax>().FirstOrDefault();
            if (returnStmt == null)
                return (null, null, null);

            if (!(returnStmt.Expression is ObjectCreationExpressionSyntax creation))
                return (null, null, null);

            if (creation.Initializer == null)
                return (null, null, null);

            string title = null;
            string description = null;
            string domainTypeName = null;

            foreach (var expr in creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
            {
                var leftName = expr.Left switch
                {
                    IdentifierNameSyntax ident => ident.Identifier.Text,
                    MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
                    _ => null
                };

                if (string.IsNullOrWhiteSpace(leftName))
                    continue;

                if (leftName.Equals("Name", StringComparison.OrdinalIgnoreCase))
                {
                    title = TryGetStringLiteral(expr.Right) ?? title;
                }
                else if (leftName.Equals("Description", StringComparison.OrdinalIgnoreCase))
                {
                    description = TryGetStringLiteral(expr.Right) ?? description;
                }
                else if (leftName.Equals("DomainType", StringComparison.OrdinalIgnoreCase))
                {
                    domainTypeName = TryGetEnumMemberName(expr.Right) ?? domainTypeName;
                }
            }

            return (title, description, domainTypeName);
        }

        private static Dictionary<string, string> BuildConstStringLookup(ClassDeclarationSyntax classDecl)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var field in classDecl.Members.OfType<FieldDeclarationSyntax>())
            {
                if (!field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
                    continue;

                if (!(field.Declaration.Type is PredefinedTypeSyntax pts) || !pts.Keyword.IsKind(SyntaxKind.StringKeyword))
                    continue;

                foreach (var variable in field.Declaration.Variables)
                {
                    var name = variable.Identifier.Text;
                    var value = TryGetStringLiteral(variable.Initializer?.Value);
                    if (!string.IsNullOrWhiteSpace(name) && value != null)
                    {
                        dict[name] = value;
                    }
                }
            }

            return dict;
        }

        private static string ResolveDomainKey(AttributeSyntax attr, Dictionary<string, string> constStrings)
        {
            if (attr.ArgumentList == null || attr.ArgumentList.Arguments.Count == 0)
                return null;

            var argExpr = attr.ArgumentList.Arguments[0].Expression;

            var literal = TryGetStringLiteral(argExpr);
            if (literal != null)
                return literal;

            if (argExpr is IdentifierNameSyntax ident)
            {
                if (constStrings.TryGetValue(ident.Identifier.Text, out var value))
                {
                    return value;
                }
            }

            return null;
        }

        private static string ResolveDomainKeyName(AttributeSyntax attr, Dictionary<string, string> constStrings)
        {
            if (attr.ArgumentList == null || attr.ArgumentList.Arguments.Count == 0)
                return null;

            var argExpr = attr.ArgumentList.Arguments[0].Expression;

            if (argExpr is IdentifierNameSyntax ident)
            {
                return ident.Identifier.Text;
            }

            return null;
        }

        private static bool HasAttribute(SyntaxList<AttributeListSyntax> lists, string attributeName)
        {
            return GetAttribute(lists, attributeName) != null;
        }

        private static AttributeSyntax GetAttribute(SyntaxList<AttributeListSyntax> lists, string attributeName)
        {
            foreach (var list in lists)
            {
                foreach (var attr in list.Attributes)
                {
                    var name = attr.Name.ToString();

                    if (name.Equals(attributeName, StringComparison.Ordinal) ||
                        name.Equals(attributeName + "Attribute", StringComparison.Ordinal) ||
                        name.EndsWith("." + attributeName, StringComparison.Ordinal) ||
                        name.EndsWith("." + attributeName + "Attribute", StringComparison.Ordinal))
                    {
                        return attr;
                    }
                }
            }

            return null;
        }

        private static bool IsStaticPublic(PropertyDeclarationSyntax prop)
        {
            var mods = prop.Modifiers;
            return mods.Any(m => m.IsKind(SyntaxKind.PublicKeyword)) &&
                   mods.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
        }

        private static bool IsDomainDescriptionType(TypeSyntax typeSyntax)
        {
            var typeName = typeSyntax.ToString();
            return typeName.EndsWith("DomainDescription", StringComparison.Ordinal);
        }

        private static string TryGetStringLiteral(ExpressionSyntax expr)
        {
            if (expr is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText;
            }

            return null;
        }

        private static string TryGetEnumMemberName(ExpressionSyntax expr)
        {
            if (expr is MemberAccessExpressionSyntax member)
            {
                return member.Name.Identifier.Text;
            }

            if (expr is IdentifierNameSyntax ident)
            {
                return ident.Identifier.Text;
            }

            return null;
        }
    }
}
