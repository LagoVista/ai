// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: bcb24283dfde843f56775a76b23a654ed6da1310b57c5d16bef9ab51f9a681c4
// IndexVersion: 2
// --- END CODE INDEX META ---
namespace LagoVista.AI.Services
{
    public sealed class Snippet
    {
        public Snippet(string tag, string path, string title, int start, int end, string text, string symbol, string symbolType)
        {
            Tag = tag;
            Path = path;
            Title = title;
            Start = start;
            End = end;
            Text = text;
            Symbol = symbol;
            SymbolType = symbolType;
        }

        public string Title { get; set; }
        public string Tag { get; }
        public string Path { get; }
        public int Start { get; }
        public int End { get; }
        public string Text { get; }
        public string Symbol { get; }
        public string SymbolType { get; }

    }
}
