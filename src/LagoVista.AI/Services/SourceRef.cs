// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: c7326d0c1d3b2e05697196ef86acdee0979920a5e15365bd3616e7baa1c326d8
// IndexVersion: 2
// --- END CODE INDEX META ---
namespace LagoVista.AI.Services
{
    public sealed class SourceRef
    {
        public string Tag { get; set; } = "";
        public string Path { get; set; } = "";
        public string FileName { get; set; } = "";
        public int Start { get; set; }
        public int End { get; set; }
        public string Excerpt { get; set; }
        public string Symbol { get; set; }
        public string SymbolType { get; set; }
    }
}
