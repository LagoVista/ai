using System;
using System.Text;
using System.Text.RegularExpressions;

public class RagContent
{
    public int RagContentIndex { get; set; }
    public string ModelContent { get; set; }
    public string HumanContentUrl { get; set; }
    public string FullContentUrl { get; set; }
    public string Title { get; set; }
    public string PointId { get; set; }
    public string ScemanticId { get; set; }

    internal string ToContentBlock(int maxChars = 4000)
    {
        // Stable citation key (R1, R2, ...)
        var cite = $"R{RagContentIndex + 1}";

        // Avoid delimiter collisions
        var safeText = (ModelContent ?? string.Empty).Trim();
        safeText = safeText.Replace("\r\n", "\n");
        safeText = safeText.Replace("\u0000", ""); // just in case
        safeText = EscapeTag(safeText, "<RAG_EXHIBIT>");
        safeText = EscapeTag(safeText, "</RAG_EXHIBIT>");

        if (safeText.Length > maxChars)
            safeText = safeText.Substring(0, maxChars) + "\n…[truncated]";

        var sb = new StringBuilder();
        sb.AppendLine($"<RAG_EXHIBIT id=\"{cite}\">");
        sb.AppendLine($"title: {NullDash(Title)}");
        sb.AppendLine($"pointId: {NullDash(PointId)}");
        sb.AppendLine($"semanticId: {NullDash(ScemanticId)}");
        sb.AppendLine($"humanUrl: {NullDash(HumanContentUrl)}");
        sb.AppendLine($"fullUrl: {NullDash(FullContentUrl)}");
        sb.AppendLine("content:");
        sb.AppendLine(safeText);
        sb.AppendLine($"</RAG_EXHIBIT>");
        return sb.ToString();

        static string NullDash(string v) => string.IsNullOrWhiteSpace(v) ? "-" : v.Trim();

        static string EscapeTag(string text, string tag)
            => Regex.Replace(text, Regex.Escape(tag), m => m.Value.Replace("<", "&lt;").Replace(">", "&gt;"));
    }
}
