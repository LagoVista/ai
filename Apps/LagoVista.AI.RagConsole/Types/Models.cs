namespace RagCli.Types
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