using System.Text.RegularExpressions;
using RagCli.Types;

namespace RagCli.Services
{
    public class SimpleChunker
    {
        private readonly int _maxTokensPerChunk;
        private readonly int _overlapLines;

        public SimpleChunker(int maxTokensPerChunk = 7000, int overlapLines = 8)
        {
            _maxTokensPerChunk = Math.Max(512, maxTokensPerChunk);
            _overlapLines = Math.Max(0, overlapLines);
        }

        public IEnumerable<CodeChunk> Chunk(string text, string relPath)
        {
            var lines = text.Split('\n');
            int start = 0;

            while (start < lines.Length)
            {
                int end = start;
                int estTokens = 0;

                // Grow the window until we exceed the token budget
                while (end < lines.Length)
                {
                    estTokens += TokenEstimator.EstimateTokens(lines[end]) + 1; // +1 for newline
                    if (estTokens > _maxTokensPerChunk)
                    {
                        if (end == start) end++; // ensure progress for huge single lines
                        break;
                    }
                    end++;
                }

                // Build the chunk
                var slice = string.Join('\n', lines[start..Math.Min(end, lines.Length)]);
                var (kind, symbol) = GuessSymbol(slice, relPath);

                yield return new CodeChunk
                {
                    Text = slice,
                    Path = relPath,
                    StartLine = start + 1,
                    EndLine = Math.Min(end, lines.Length),
                    Kind = kind,
                    Symbol = symbol
                };

                // Slide the window forward with overlap
                start = end - _overlapLines;
                if (start < 0) start = 0;
                if (start >= lines.Length) break;
            }
        }

        private static (string Kind, string Symbol) GuessSymbol(string block, string path)
        {
            var patterns = new (string kind, string rx)[]
            {
                ("class", @"class\s+([A-Za-z_][A-Za-z0-9_]*)"),
                ("function", @"(public|private|protected|static|async|function)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\("),
                ("method", @"[A-Za-z_][A-Za-z0-9_]*\s*\(.*\)\s*{"),
                ("heading", @"^#+\s+(.+)$")
            };

            foreach (var (k, rx) in patterns)
            {
                var m = Regex.Match(block, rx, RegexOptions.Multiline);
                if (m.Success) return (k, m.Groups.Count > 1 ? m.Groups[^1].Value : "");
            }

            return ("block", Path.GetFileName(path));
        }
    }
}
