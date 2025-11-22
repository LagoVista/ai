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
    /// 1) Split symbols
    /// 2) Analyze each independently (SubKind, metadata, etc)
    /// </summary>
    public static class SymbolSplitter
    {
        public static InvokeResult<IReadOnlyList<SplitSymbolResult>> Split(string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText)) throw new ArgumentNullException(nameof(sourceText));

            var tree = CSharpSyntaxTree.ParseText(sourceText);
            var root = tree.GetRoot() as CompilationUnitSyntax;

            if (root == null)
                return InvokeResult<IReadOnlyList<SplitSymbolResult>>.FromError("Could not identify root in source code");

            var symbols = root.DescendantNodes()
                .Where(n => n is TypeDeclarationSyntax || n is EnumDeclarationSyntax)
                .ToList();

            var results = new List<SplitSymbolResult>();

            foreach (var node in symbols)
            {
                var name = GetSymbolName(node);
                var kind = node.Kind().ToString();

                var isolated = BuildIsolatedSymbolText(node, root);

                results.Add(new SplitSymbolResult
                {
                    SymbolName = name,
                    SymbolKind = kind,
                    Text = isolated
                });
            }

            // Handle no type case
            if (results.Count == 0)
            {
                return InvokeResult<IReadOnlyList<SplitSymbolResult>>.FromError("Did not identify any symbols within source code text");
            }

            return  InvokeResult<IReadOnlyList<SplitSymbolResult>>.Create(results);
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

            if (!string.IsNullOrWhiteSpace(ns))
            {
                if (sb.Length > 0) sb.AppendLine();

                sb.Append("namespace ").AppendLine(ns);
                sb.AppendLine("{");

                var inner = node.ToFullString().Replace("\r\n", "\n").Split('\n');
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
                sb.AppendLine(node.ToFullString());
            }

            return sb.ToString();
        }
    }

    public sealed class SplitSymbolResult
    {
        public string SymbolName { get; set; }
        public string SymbolKind { get; set; }
        public string Text { get; set; }
    }
}
