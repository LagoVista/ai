namespace LagoVista.AI.Services
{
    public sealed class Snippet
    {
        public Snippet(string tag, string path, string fileName, int start, int end, string text, string symbol, string symbolType)
        {
            Tag = tag;
            Path = path;
            FileName = fileName;
            Start = start;
            End = end;
            Text = text;
            Symbol = symbol;
            SymbolType = symbolType;
        }

        public string Tag { get; }
        public string Path { get; }
        public string FileName { get; }
        public int Start { get; }
        public int End { get; }
        public string Text { get; }
        public string Symbol { get; }
        public string SymbolType { get; }

    }
}
