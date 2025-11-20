// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 8d79e3d886d46838a4a7e390cc8157c2c560b3df43e20a7c800b856a151c2979
// IndexVersion: 2
// --- END CODE INDEX META ---
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Services;
using LagoVista.Core.Utils;
using LagoVista.Core.Utils.Types;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Symbol-aware C# chunker using Roslyn. Produces chunks for types and members
    /// with line numbers and token-aware size limiting.
    /// Includes robust cursor-advance logic to avoid infinite loops even with
    /// extreme settings (e.g., tiny token budgets or large overlaps).
    /// </summary>
    public class RoslynCSharpChunker
    {
        private readonly int _maxTokensPerChunk;
        private readonly int _overlapLines;

        public RoslynCSharpChunker(int maxTokensPerChunk = 6500, int overlapLines = 6)
        {
            _maxTokensPerChunk = Math.Max(128, maxTokensPerChunk);
            _overlapLines = Math.Max(0, overlapLines);
        }

        /// <summary>
        /// Chunk a C# file into symbol-aligned, token-budgeted chunks.
        /// </summary>
        public RagChunkPlan Chunk(string text, string relPath, string blobPath)
        {
            var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Preview));
            var root = tree.GetRoot();
            var lines = text.Split('\n');
            var chunks = new List<RagChunk>();

            foreach (var summary in BuildFileSummaryChunk(tree, relPath, lines))
                chunks.Add(summary);


            // Walk declarations and emit symbol-level chunks
            foreach (var node in root.DescendantNodes())
            {
                switch (node)
                {
                    case BaseMethodDeclarationSyntax m: // method, ctor, destructor, operator
                        foreach (var ch in BuildNodeChunks(tree, lines, relPath, m, kind: "method"))
                            chunks.Add(ch);
                        break;

                    case PropertyDeclarationSyntax p:
                        foreach (var ch in BuildNodeChunks(tree, lines, relPath, p, kind: "property"))
                            chunks.Add(ch);
                        break;

                    case FieldDeclarationSyntax f when f.Parent is TypeDeclarationSyntax:
                        foreach (var ch in BuildNodeChunks(tree, lines, relPath, f, kind: "field"))
                            chunks.Add(ch);
                        break;

                    case EventDeclarationSyntax e:
                        foreach (var ch in BuildNodeChunks(tree, lines, relPath, e, kind: "event"))
                            chunks.Add(ch);
                        break;

                    case TypeDeclarationSyntax t: // class/record/struct/interface
                        foreach (var ch in BuildNodeChunks(tree, lines, relPath, t, kind: t.Keyword.ValueText))
                            chunks.Add(ch);
                        break;
                }
            }

            var idx = 1;
            foreach(var chunk in chunks)
            {
                chunk.PartIndex = idx++;
                chunk.PartTotal = chunks.Count;
            }

            return new RagChunkPlan()
            {
                Chunks = chunks,
                Raw = new RawArtifact()
                {
                    IsText = true,
                    MimeType = "text/x-csharp",
                    SuggestedBlobPath = blobPath,
                    Text = text,

                },
            };
        }

        private IEnumerable<RagChunk> BuildFileSummaryChunk(SyntaxTree tree, string relPath, string[] lines)
        {
            var root = tree.GetRoot();
            var sb = new StringBuilder();

            var headerTrivia = root.GetLeadingTrivia()
                .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia));
            if (headerTrivia != default)
            {
                sb.AppendLine("// file header comment:");
                sb.AppendLine(headerTrivia.ToString());
                sb.AppendLine();
            }

            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().Select(u => u.ToString()).ToList();
            if (usings.Count > 0)
            {
                sb.AppendLine("// usings:");
                foreach (var u in usings.Take(20)) sb.AppendLine(u);
            }

            var text = sb.ToString().TrimEnd();
            if(text.Length > 0x7fff)
            {
                Debugger.Break();
                text = text.Substring(0, 0x7fff);
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                var estTokens = TokenEstimator.EstimateTokens(text); // +1 newline
                while(estTokens > _maxTokensPerChunk)
                {
                    text = text.Substring(0, Convert.ToInt32(text.Length * 0.90));    
                    estTokens = TokenEstimator.EstimateTokens(text); // +1 newline
                }

                yield return new RagChunk
                { 
                    EstimatedTokens = TokenEstimator.EstimateTokens(text),
                    TextNormalized = text,
                    LineStart = 1,
                    LineEnd = Math.Min(lines.Length, 200),
                    Symbol = System.IO.Path.GetFileName(relPath),
                    SymbolType = "file",
                    SectionKey = "file",
                };
            }
        }

        private IEnumerable<RagChunk> BuildNodeChunks(
            SyntaxTree tree, string[] lines, string relPath, SyntaxNode node, string kind)
        {
            // Prefer mapped span (accounts for directives) if available
            var mapped = tree.GetMappedLineSpan(node.Span);
            var span = mapped.IsValid ? mapped : tree.GetLineSpan(node.Span);

            int startLine = Math.Max(1, span.StartLinePosition.Line + 1);
            int endLine = Math.Max(startLine, span.EndLinePosition.Line + 1);

            // Expand to include XML docs and attributes that precede the node
            var leading = node.GetLeadingTrivia().ToFullString();
            if (!string.IsNullOrEmpty(leading))
                startLine = Math.Max(1, startLine - CountLines(leading));

            // Clamp to file bounds
            startLine = Math.Min(startLine, lines.Length > 0 ? lines.Length : 1);
            endLine = Math.Min(endLine, lines.Length);

            int cursor = startLine - 1;
            int safety = 0;
            const int safetyCap = 1_000_000;

            while (cursor < endLine && cursor < lines.Length)
            {
                if (safety++ > safetyCap) yield break;

                int estTokens = 0;
                int localStart = cursor;
                int localEnd = cursor;

                while (localEnd < endLine && localEnd < lines.Length)
                {
                    var line = lines[localEnd];

                    estTokens += TokenEstimator.EstimateTokens(line) + 1; // +1 newline
                    if (estTokens > _maxTokensPerChunk)
                    {
                        // If a single line already exceeds the budget, slice it safely
                        if (localEnd == localStart)
                        {
                            var parts = SliceVeryLongLine(
                                lines[localEnd],
                                relPath,
                                kind,
                                GetBestSymbolName(node),
                                localEnd + 1);

                            foreach (var sl in parts)
                            {
                                yield return sl;
                            }

                            // Advance cursor past this very long line
                            cursor = localEnd + 1;
                            goto nextIteration;
                        }
                        break;
                    }
                    localEnd++;
                }

              
                // Emit normal chunk
                var slice = string.Join('\n', lines[localStart..Math.Min(localEnd, lines.Length)]);
                yield return new RagChunk
                {
                    EstimatedTokens = TokenEstimator.EstimateTokens(slice),
                    TextNormalized = slice,
                    LineStart = localStart + 1,
                    LineEnd = Math.Min(localEnd, lines.Length),
                    Symbol = GetBestSymbolName(node),
                    SymbolType = kind,
                    SectionKey = kind,
                    
                };

                // Advance with overlap; ensure forward progress
                int nextCursor = localEnd - _overlapLines;
                if (nextCursor <= cursor) nextCursor = localEnd;
                if (nextCursor <= cursor) nextCursor = cursor + 1;
                cursor = nextCursor;

            nextIteration:;
            }
        }

        /// <summary>
        /// Slices an extremely long single line into multiple safe chunks,
        /// keeping each segment well under the token budget.
        /// We report the same StartLine/EndLine for all segments (the original line),
        /// so consumers can still locate the source reliably.
        /// </summary>
        private IEnumerable<RagChunk> SliceVeryLongLine(
            string line,
            string relPath,
            string kind,
            string symbol,
            int lineNumber)
        {
            // Rough char window derived from token budget (tokens ≈ bytes/4 ≈ chars for ASCII-ish)
            // Keep it conservative and leave headroom.
            int windowChars = Math.Max(512, _maxTokensPerChunk * 3); // 3x is a safe heuristic
            int idx = 0;
            int seg = 1;

            // Try to break at natural boundaries inside each window
            while (idx < line.Length)
            {
                int remaining = line.Length - idx;
                int take = Math.Min(windowChars, remaining);

                // Look for a soft break near the end of the window to avoid cutting identifiers
                int softBreak = FindSoftBreak(line, idx, take);
                if (softBreak > 0) take = softBreak;

                var piece = line.AsSpan(idx, take).ToString();
                yield return new RagChunk
                {
                    EstimatedTokens = TokenEstimator.EstimateTokens(piece),
                    TextNormalized = piece,
                    LineStart = lineNumber,
                    LineEnd = lineNumber,
                    Symbol = symbol,
                    SectionKey = kind,
                    SymbolType = kind,// same symbol; UI can show "(continued)" if needed

                };

                idx += take;
                seg++;
            }
        }

        /// <summary>
        /// Try to find a nicer split point near the window end: whitespace, comma, semicolon, brace, or bracket.
        /// Searches backwards up to ~80 chars from the window end. Returns number of chars to take if found; else 0.
        /// </summary>
        private static int FindSoftBreak(string s, int start, int window)
        {
            int end = Math.Min(start + window, s.Length);
            int scanFrom = Math.Max(start, end - 80); // look back a bit

            for (int i = end - 1; i >= scanFrom; i--)
            {
                var endLimitChars = new List<char>() { ',', ';', ')', ']', '}' };

                char c = s[i];
                if (char.IsWhiteSpace(c) || endLimitChars.Contains(c)) 
                {
                    return (i + 1) - start;
                }
            }
            return 0;
        }


        private static int CountLines(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int count = 0;
            foreach (var ch in s)
                if (ch == '\n') count++;
            return Math.Max(1, count);
        }

        private static string GetBestSymbolName(SyntaxNode node)
        {
            return node switch
            {
                MethodDeclarationSyntax m => m.Identifier.ValueText,
                ConstructorDeclarationSyntax c => c.Identifier.ValueText,
                PropertyDeclarationSyntax p => p.Identifier.ValueText,
                FieldDeclarationSyntax f => f.Declaration.Variables.FirstOrDefault()?.Identifier.ValueText ?? "field",
                BaseTypeDeclarationSyntax t => t.Identifier.ValueText,
                _ => node.Kind().ToString()
            };
        }
    }
}
