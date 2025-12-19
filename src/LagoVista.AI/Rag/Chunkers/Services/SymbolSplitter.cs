using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LagoVista.Core.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// Splits a C# file into isolated symbol-level segments.
    /// Each result includes using statements + namespace + symbol body.
    ///
    /// This is intentionally separate from SubKind analysis so the pipeline can
    /// 1) Split symbols (including nested types)
    /// 2) Analyze each independently (SubKind, metadata, etc)
    /// </summary>
    public static class SymbolSplitter
    {
        public static InvokeResult<IReadOnlyList<SplitSymbolResult>> Split(string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
                throw new ArgumentNullException(nameof(sourceText));

            var tree = CSharpSyntaxTree.ParseText(sourceText);
            var root = tree.GetRoot() as CompilationUnitSyntax;

            if (root == null)
            {
                return InvokeResult<IReadOnlyList<SplitSymbolResult>>
                    .FromError("Could not identify root in source code");
            }

            // Types & enums, including nested ones.
            var symbols = root
                .DescendantNodes()
                .Where(n => n is TypeDeclarationSyntax || n is EnumDeclarationSyntax)
                .ToList();

            var results = new List<SplitSymbolResult>();

            foreach (var node in symbols)
            {
                var name = GetSymbolName(node);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var kind = node.Kind().ToString();
                var ns = GetNamespace(node);
                var declaringTypeChain = GetDeclaringTypeChain(node);
                var fullName = BuildFullName(ns, declaringTypeChain, name);
                var isolated = BuildIsolatedSymbolText(node, root);

                results.Add(new SplitSymbolResult
                {
                    SymbolName = name,
                    SymbolKind = kind,
                    Namespace = ns,
                    DeclaringTypePath = declaringTypeChain.Count == 0
                        ? null
                        : string.Join(".", declaringTypeChain),
                    FullName = fullName,
                    Text = isolated
                });
            }

            if (results.Count == 0)
            {
                return InvokeResult<IReadOnlyList<SplitSymbolResult>>
                    .FromError("Did not identify any symbols within source code text");
            }

            return InvokeResult<IReadOnlyList<SplitSymbolResult>>.Create(results);
        }

        private static string GetSymbolName(SyntaxNode node)
        {
            return node switch
            {
                ClassDeclarationSyntax c => c.Identifier.ValueText,
                StructDeclarationSyntax s => s.Identifier.ValueText,
                RecordDeclarationSyntax r => r.Identifier.ValueText,
                InterfaceDeclarationSyntax i => i.Identifier.ValueText,
                EnumDeclarationSyntax e => e.Identifier.ValueText,
                _ => null
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

        /// <summary>
        /// Returns the chain of containing types for a node, from outermost to innermost.
        /// For YearEndTaxes.Category this returns ["YearEndTaxes"].
        /// </summary>
        private static List<string> GetDeclaringTypeChain(SyntaxNode node)
        {
            return node
                .Ancestors()
                .OfType<TypeDeclarationSyntax>()
                .Select(t => t.Identifier.ValueText)
                .Reverse()
                .ToList();
        }

        private static string BuildFullName(string @namespace, List<string> declaringTypeChain, string name)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(@namespace))
            {
                sb.Append(@namespace);
                sb.Append('.');
            }

            if (declaringTypeChain != null && declaringTypeChain.Count > 0)
            {
                sb.Append(string.Join(".", declaringTypeChain));
                sb.Append('.');
            }

            sb.Append(name);
            return sb.ToString();
        }

        /// <summary>
        /// Builds isolated compilable text for the symbol:
        /// - Includes relevant using statements.
        /// - Wraps in namespace if present.
        /// - Strips nested type/enum declarations so each symbol is unique.
        /// </summary>
        private static string BuildIsolatedSymbolText(SyntaxNode node, CompilationUnitSyntax root)
        {
            var sb = new StringBuilder();

            // Get all relevant using statements
            var usingNodes = new List<UsingDirectiveSyntax>();
            usingNodes.AddRange(root.Usings);

            var namespaceNode = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
            if (namespaceNode != null)
                usingNodes.AddRange(namespaceNode.Usings);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var u in usingNodes)
            {
                var txt = u.ToString().Trim();
                if (seen.Add(txt))
                    sb.AppendLine(txt);
            }

            var ns = GetNamespace(node);

            // Strip nested types/enums from this symbol; they will get their own SplitSymbolResult.
            var strippedNode = StripNestedTypes(node);

            if (!string.IsNullOrWhiteSpace(ns))
            {
                if (sb.Length > 0) sb.AppendLine();

                sb.Append("namespace ").AppendLine(ns);
                sb.AppendLine("{");

                // Normalize to LF before indenting to avoid weird spacing.
                var inner = strippedNode.ToFullString().Replace("\r\n", "\n").Split('\n');
                foreach (var line in inner)
                {
                    if (line.Length > 0)
                        sb.Append("    ");

                    sb.AppendLine(line);
                }

                sb.AppendLine("}");
            }
            else
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine(strippedNode.ToFullString());
            }

            return sb.ToString();
        }

        /// <summary>
        /// Removes any nested type/enum declarations that are descendants of this node,
        /// leaving only the current symbol's own body. This prevents duplicates between
        /// host and nested symbols.
        /// </summary>
        private static SyntaxNode StripNestedTypes(SyntaxNode node)
        {
            // Find all nested type/enum declarations that are descendants but not the node itself.
            var nested = node
                .DescendantNodes()
                .Where(n =>
                    (n is TypeDeclarationSyntax || n is EnumDeclarationSyntax) &&
                    !ReferenceEquals(n, node))
                .ToList();

            if (nested.Count == 0)
                return node;

            return node.RemoveNodes(nested, SyntaxRemoveOptions.KeepNoTrivia);
        }
    }

    public sealed class SplitSymbolResult
    {
        /// <summary>
        /// Simple name of the symbol (e.g. "YearEndTaxes", "TaxCategory").
        /// </summary>
        public string SymbolName { get; set; }

        /// <summary>
        /// Roslyn kind (e.g. "ClassDeclaration", "StructDeclaration", "EnumDeclaration").
        /// </summary>
        public string SymbolKind { get; set; }

        /// <summary>
        /// Namespace the symbol lives in, if any.
        /// e.g. "LagoVista.IoT.Billing.Models".
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// Dot-separated path of containing types (outermost → innermost), if any.
        /// e.g. "YearEndTaxes" for YearEndTaxes.TaxCategory.
        /// </summary>
        public string DeclaringTypePath { get; set; }

        /// <summary>
        /// Fully-qualified name including namespace and declaring types.
        /// e.g. "LagoVista.IoT.Billing.Models.YearEndTaxes.TaxCategory".
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// Isolated compilable text: usings + namespace + symbol body
        /// (with nested type declarations removed).
        /// </summary>
        public string Text { get; set; }
    }
}
