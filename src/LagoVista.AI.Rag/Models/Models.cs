// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 7335a95b713dc120951347c43b98d7614d4d55e0dec3f1f734ea6c8ba6f0b923
// IndexVersion: 2
// --- END CODE INDEX META ---
using System.IO;

namespace LagoVista.AI.Rag.Models
{
    public static class LanguageGuesser
    {
        public static string FromPath(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".cs" => "csharp",
                ".ts" => "typescript",
                ".js" => "javascript",
                ".md" => "markdown",
                _ => ext.TrimStart('.')
            };
        }
    }

    public class CodeChunk
    {
        public string Text { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public string Kind { get; set; } = "block";   // class/function/method/block
        public string Symbol { get; set; } = string.Empty;
    }

}