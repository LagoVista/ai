using System.Text.RegularExpressions;
using RagCli.Types;

namespace RagCli.Services
{
    public class SimpleChunker
    {
        // Very simple symbol-ish chunker: split by large code blocks or headings; attach line numbers
        public IEnumerable<CodeChunk> Chunk(string text, string relPath, int maxLines = 120)
        {
            var lines = text.Split('\n');
            int start = 0;
            while (start < lines.Length)
            {
                int end = Math.Min(start + maxLines, lines.Length);
                var slice = string.Join('\n', lines[start..end]);
                var (kind, symbol) = GuessSymbol(slice, relPath);
                yield return new CodeChunk
                {
                    Text = slice,
                    Path = relPath,
                    StartLine = start + 1,
                    EndLine = end,
                    Kind = kind,
                    Symbol = symbol
                };
                start = end;
            }
        }

        private static (string Kind, string Symbol) GuessSymbol(string block, string path)
        {
            // Extremely naive: look for function/class signatures in common langs
            var patterns = new (string kind, string rx)[]
            {
                ("class", @"class\s+([A-Za-z_][A-Za-z0-9_]*)"),
                ("function", @"(public|private|protected|static|async|function)\s+[A-Za-z_][A-Za-z0-9_]*\s*\("),
                ("method", @"[A-Za-z_][A-Za-z0-9_]*\s*\(.*\)\s*{"),
                ("heading", @"^#+\s+(.+)$")
            };
            foreach (var (k, rx) in patterns)
            {
                var m = Regex.Match(block, rx, RegexOptions.Multiline);
                if (m.Success) return (k, m.Groups.Count > 1 ? m.Groups[1].Value : "");
            }
            return ("block", Path.GetFileName(path));
        }
    }
}
