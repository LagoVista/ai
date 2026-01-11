using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using LagoVista.AI.Indexing.Utils;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// Symbol-aware C# chunker using Roslyn. Produces chunks for types and members
    /// with line numbers, character ranges, and token-aware size limiting.
    /// Includes robust cursor-advance logic to avoid infinite loops even with
    /// extreme settings (e.g., tiny token budgets or large overlaps).
    /// </summary>
    public static class RoslynCSharpChunker
    {
        /// <summary>
        /// Chunk a C# file into a collection of CSharpComponentChunk, including:
        /// - A file-level summary chunk (header comments + using directives)
        /// - Symbol-level chunks for types/members (methods, properties, fields, etc.)
        ///
        /// Chunks are roughly bounded by maxTokensPerChunk using a simple token estimator.
        /// Overlap is applied between symbol chunks to give the embedder continuity.
        /// </summary>
        public static InvokeResult<IReadOnlyList<CSharpComponentChunk>> Chunk(
            string text,
            string fileName,
            int maxTokensPerChunk = 4096,
            int overlapLines = 6)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return InvokeResult<IReadOnlyList<CSharpComponentChunk>>
                    .FromError("Text is required.");
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "unknown.cs";
            }

            if (maxTokensPerChunk <= 0)
            {
                maxTokensPerChunk = 4096;
            }

            if (overlapLines < 0)
            {
                overlapLines = 0;
            }

            var result = new InvokeResult<IReadOnlyList<CSharpComponentChunk>>();

            try
            {
                var tree = CSharpSyntaxTree.ParseText(text);
                var root = tree.GetRoot();
                var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                var lineStartOffsets = ComputeLineStartOffsets(text);
                var textLength = text.Length;

                var chunks = new List<CSharpComponentChunk>();

                // 1) File-level summary chunk for header comments + using directives
                foreach (var summary in BuildFileSummaryChunk(
                             fileName,
                             tree,
                             lines,
                             lineStartOffsets,
                             textLength,
                             maxTokensPerChunk))
                {
                    chunks.Add(summary);
                }

                // 2) Symbol-level chunks
                foreach (var node in root.DescendantNodes())
                {
                    switch (node.GetType().Name)
                    {
                        case nameof(MethodDeclarationSyntax):
                        case nameof(ConstructorDeclarationSyntax):
                        case nameof(LocalFunctionStatementSyntax):
                        case nameof(OperatorDeclarationSyntax):
                        case nameof(ConversionOperatorDeclarationSyntax):
                            foreach (var ch in BuildNodeChunks(
                                         tree,
                                         lines,
                                         lineStartOffsets,
                                         node,
                                         "method",
                                         maxTokensPerChunk,
                                         overlapLines,
                                         textLength))
                            {
                                chunks.Add(ch);
                            }
                            break;

                        case nameof(PropertyDeclarationSyntax):
                            foreach (var ch in BuildNodeChunks(
                                         tree,
                                         lines,
                                         lineStartOffsets,
                                         node,
                                         "property",
                                         maxTokensPerChunk,
                                         overlapLines,
                                         textLength))
                            {
                                chunks.Add(ch);
                            }
                            break;

                        case nameof(FieldDeclarationSyntax):
                            foreach (var ch in BuildNodeChunks(
                                         tree,
                                         lines,
                                         lineStartOffsets,
                                         node,
                                         "field",
                                         maxTokensPerChunk,
                                         overlapLines,
                                         textLength))
                            {
                                chunks.Add(ch);
                            }
                            break;

                        case nameof(EventDeclarationSyntax):
                        case nameof(EventFieldDeclarationSyntax):
                            foreach (var ch in BuildNodeChunks(
                                         tree,
                                         lines,
                                         lineStartOffsets,
                                         node,
                                         "event",
                                         maxTokensPerChunk,
                                         overlapLines,
                                         textLength))
                            {
                                chunks.Add(ch);
                            }
                            break;

                        case nameof(ClassDeclarationSyntax):
                        case nameof(StructDeclarationSyntax)    :
                        case nameof(RecordDeclarationSyntax):
                        case nameof(InterfaceDeclarationSyntax):
                        case nameof(EnumDeclarationSyntax):
                            foreach (var ch in BuildNodeChunks(
                                         tree,
                                         lines,
                                         lineStartOffsets,
                                         node,
                                         "type",
                                         maxTokensPerChunk,
                                         overlapLines,
                                         textLength))
                            {
                                chunks.Add(ch);
                            }
                            break;
                    }
                }

                // Assign PartIndex/PartTotal deterministically
                var idx = 1;
                var total = chunks.Count;
                foreach (var chunk in chunks)
                {
                    chunk.PartIndex = idx++;
                    chunk.PartTotal = total;
                }

                result.Result = chunks;
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                return InvokeResult<IReadOnlyList<CSharpComponentChunk>>
                    .FromException("Failed to chunk C# source.", ex);
            }
        }

        /// <summary>
        /// Builds a file-level summary chunk that includes:
        /// - Leading comments (license header, file description, etc.)
        /// - Using directives
        /// </summary>
        private static IEnumerable<CSharpComponentChunk> BuildFileSummaryChunk(
            string fileName,
            SyntaxTree tree,
            string[] lines,
            int[] lineStartOffsets,
            int textLength,
            int maxTokensPerChunk)
        {
            var root = tree.GetRoot();
            var sb = new StringBuilder();

            // Collect leading comments
            var leadingTrivia = root.GetLeadingTrivia();
            foreach (var trivia in leadingTrivia)
            {
                if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                    trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                    trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                    trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                {
                    sb.Append(trivia.ToFullString());
                }
                else if (trivia.IsKind(SyntaxKind.WhitespaceTrivia) ||
                         trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    sb.Append(trivia.ToFullString());
                }
                else
                {
                    // Stop once we hit non-comment, non-whitespace trivia
                    break;
                }
            }

            // Collect using directives
            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
            if (usings.Count > 0)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                foreach (var u in usings)
                {
                    sb.Append(u.ToFullString());
                }
            }

            var headerText = sb.ToString().Trim();
            if (string.IsNullOrWhiteSpace(headerText))
            {
                yield break;
            }

            // Enforce token budget by trimming if necessary (rare)
            var estTokens = TokenEstimator.EstimateTokens(headerText);
            while (estTokens > maxTokensPerChunk && headerText.Length > 0)
            {
                var newLen = (int)(headerText.Length * 0.9);
                if (newLen <= 0) break;
                headerText = headerText.Substring(0, newLen);
                estTokens = TokenEstimator.EstimateTokens(headerText);
            }

            if (string.IsNullOrWhiteSpace(headerText))
            {
                yield break;
            }

            // Map to a line and character range:
            // we approximate by spanning the top of the file (up to 200 lines)
            var lineStart = 1;
            var lineEnd = Math.Min(lines.Length, 200);

            int startChar = 0;
            int endChar = textLength;

            if (lineStartOffsets.Length > 0)
            {
                startChar = lineStartOffsets[0];

                var lastLineIndex = Math.Min(lineEnd, lineStartOffsets.Length) - 1;
                if (lastLineIndex < 0) lastLineIndex = 0;

                if (lastLineIndex + 1 < lineStartOffsets.Length)
                {
                    endChar = lineStartOffsets[lastLineIndex + 1];
                }
                else
                {
                    endChar = textLength;
                }
            }

            yield return new CSharpComponentChunk
            {
                EstimatedTokens = TokenEstimator.EstimateTokens(headerText),
                Text = headerText,
                LineStart = lineStart,
                LineEnd = lineEnd,
                StartCharacter = startChar,
                EndCharacter = endChar,
                SymbolKind = "file",
                SymbolName = fileName,
                SectionKey = "file"
            };
        }

        private static IEnumerable<CSharpComponentChunk> BuildNodeChunks(
            SyntaxTree tree,
            string[] lines,
            int[] lineStartOffsets,
            SyntaxNode node,
            string kind,
            int maxTokensPerChunk,
            int overlapLines,
            int textLength)
        {
            // Prefer mapped span (accounts for directives) if available
            var mapped = tree.GetMappedLineSpan(node.Span);
            var span = mapped.IsValid ? mapped : tree.GetLineSpan(node.Span);

            int startLine = Math.Max(1, span.StartLinePosition.Line + 1);
            int endLine = Math.Max(startLine, span.EndLinePosition.Line + 1);

            // Expand to include XML docs and attributes that precede the node
            var leading = node.GetLeadingTrivia().ToFullString();
            if (!string.IsNullOrEmpty(leading))
            {
                startLine = Math.Max(1, startLine - CountLines(leading));
            }

            // Clamp to file bounds
            startLine = Math.Min(startLine, lines.Length > 0 ? lines.Length : 1);
            endLine = Math.Min(endLine, lines.Length);

            int cursor = startLine - 1; // 0-based index into lines[]
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
                    if (estTokens > maxTokensPerChunk)
                    {
                        // If a single line already exceeds the budget, slice it safely
                        if (localEnd == localStart)
                        {
                            var parts = SliceVeryLongLine(
                                lines[localEnd],
                                kind,
                                GetBestSymbolName(node),
                                localEnd + 1,
                                maxTokensPerChunk,
                                lineStartOffsets,
                                textLength);

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
                var lastLineIndex = Math.Min(localEnd, lines.Length) - 1;
                if (lastLineIndex < localStart) lastLineIndex = localStart;

                var slice = string.Join('\n', lines[localStart..Math.Min(localEnd, lines.Length)]);
                var textForChunk = slice;

                // Optionally prepend a natural-language summary header for methods
                if (kind == "method" && node is BaseMethodDeclarationSyntax methodNode)
                {
                    string methodName;
                    string signature;

                    if (methodNode is MethodDeclarationSyntax m)
                    {
                        methodName = m.Identifier.ValueText;
                        signature = $"{m.ReturnType} {m.Identifier.ValueText}{m.ParameterList}";
                    }
                    else if (methodNode is ConstructorDeclarationSyntax c)
                    {
                        methodName = c.Identifier.ValueText;
                        signature = $"{c.Identifier.ValueText}{c.ParameterList}";
                    }
                    else
                    {
                        methodName = GetBestSymbolName(node);
                        signature = methodNode.ToString();
                    }

                    var ctx = new MethodSummaryContext
                    {
                        MethodName = methodName,
                        Signature = signature
                        // DomainName, ModelName, SubKind, etc. can be threaded in later
                    };

                    var headerComment = MethodSummaryBuilder.BuildHeaderComment(ctx);
                    textForChunk = headerComment + Environment.NewLine + slice;
                }

                var estimatedTokens = TokenEstimator.EstimateTokens(textForChunk);

                // Character range based on line offsets
                int charStart = 0;
                int charEnd = textLength;

                if (lineStartOffsets.Length > 0 && localStart < lineStartOffsets.Length)
                {
                    charStart = lineStartOffsets[localStart];

                    var lastIncludedLineIndex = lastLineIndex;
                    if (lastIncludedLineIndex + 1 < lineStartOffsets.Length)
                    {
                        charEnd = lineStartOffsets[lastIncludedLineIndex + 1];
                    }
                    else
                    {
                        charEnd = textLength;
                    }
                }

                yield return new CSharpComponentChunk
                {
                    EstimatedTokens = estimatedTokens,
                    Text = textForChunk,
                    LineStart = localStart + 1,
                    LineEnd = lastLineIndex + 1,
                    StartCharacter = charStart,
                    EndCharacter = charEnd,
                    SymbolName = GetBestSymbolName(node),
                    SymbolKind = kind,
                    SectionKey = kind
                };

                // Advance with overlap; ensure forward progress
                int nextCursor = localEnd - overlapLines;
                if (nextCursor <= cursor) nextCursor = localEnd;
                if (nextCursor <= cursor) nextCursor = cursor + 1;
                cursor = nextCursor;

            nextIteration:;
            }
        }

        /// <summary>
        /// Slices an extremely long single line into multiple safe chunks,
        /// keeping each segment well under the token budget.
        /// We report line numbers and character offsets for each segment.
        /// </summary>
        private static IEnumerable<CSharpComponentChunk> SliceVeryLongLine(
            string line,
            string kind,
            string symbol,
            int lineNumber,
            int maxTokensPerChunk,
            int[] lineStartOffsets,
            int textLength)
        {
            if (string.IsNullOrEmpty(line))
            {
                yield break;
            }

            int baseOffset = 0;
            if (lineStartOffsets.Length >= lineNumber)
            {
                baseOffset = lineStartOffsets[lineNumber - 1];
            }

            int cursor = 0;
            int safety = 0;
            const int safetyCap = 1_000_000;

            while (cursor < line.Length)
            {
                if (safety++ > safetyCap) yield break;

                int remaining = line.Length - cursor;
                if (remaining <= 0) yield break;

                // Start with a rough char window (tokens ~ chars/4 as a simple heuristic)
                int window = Math.Min(remaining, maxTokensPerChunk * 4);

                var segment = line.Substring(cursor, window);
                int segmentTokens = TokenEstimator.EstimateTokens(segment);

                // If still too big, shrink; if quite small, we can attempt to expand (but we keep it simple here).
                while (segmentTokens > maxTokensPerChunk && window > 1)
                {
                    window = (int)(window * 0.9);
                    segment = line.Substring(cursor, window);
                    segmentTokens = TokenEstimator.EstimateTokens(segment);
                }

                // Try to find a soft break: whitespace or punctuation near the end of the window
                int softBreak = FindSoftBreak(line, cursor, window);
                if (softBreak > 0)
                {
                    window = softBreak;
                    segment = line.Substring(cursor, window);
                    segmentTokens = TokenEstimator.EstimateTokens(segment);
                }

                int startChar = baseOffset + cursor;
                int endChar = baseOffset + cursor + window;
                if (endChar > textLength) endChar = textLength;

                yield return new CSharpComponentChunk
                {
                    EstimatedTokens = segmentTokens,
                    Text = segment,
                    LineStart = lineNumber,
                    LineEnd = lineNumber,
                    StartCharacter = startChar,
                    EndCharacter = endChar,
                    SymbolName = symbol,
                    SymbolKind = kind,
                    SectionKey = kind
                };

                cursor += window;
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
                var endLimitChars = new List<char> { ',', ';', ')', ']', '}' };

                if (char.IsWhiteSpace(s[i]) || endLimitChars.Contains(s[i]))
                {
                    return i - start + 1;
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

        /// <summary>
        /// Computes 0-based start character offsets for each line in the file.
        /// Line i (1-based) starts at index lineStartOffsets[i-1].
        /// </summary>
        private static int[] ComputeLineStartOffsets(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<int>();

            var starts = new List<int> { 0 };
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n' && i + 1 < text.Length)
                {
                    starts.Add(i + 1);
                }
            }

            return starts.ToArray();
        }
    }
}
