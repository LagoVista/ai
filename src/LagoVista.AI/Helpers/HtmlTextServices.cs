using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace LagoVista.AI.Helpers
{
    public static class HtmlTextNormalizer
    {
        public static string ToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return html;

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var sb = new StringBuilder();
                AppendNodeText(doc.DocumentNode, sb);

                var text = sb.ToString();
                text = WebUtility.HtmlDecode(text);
                text = Regex.Replace(text, @"\s+", " ").Trim();

                return text;
            }
            catch
            {
                // Fallback: strip tags (worst case, but safe)
                var noTags = Regex.Replace(html, "<.*?>", " ");
                noTags = WebUtility.HtmlDecode(noTags);
                return Regex.Replace(noTags, @"\s+", " ").Trim();
            }
        }

        private static void AppendNodeText(HtmlNode node, StringBuilder sb)
        {
            foreach (var child in node.ChildNodes)
            {
                switch (child.Name)
                {
                    case "li":
                        sb.Append("- ");
                        AppendNodeText(child, sb);
                        sb.AppendLine();
                        break;

                    case "p":
                    case "div":
                    case "br":
                        AppendNodeText(child, sb);
                        sb.AppendLine();
                        break;

                    default:
                        if (child.NodeType == HtmlNodeType.Text)
                            sb.Append(child.InnerText);

                        AppendNodeText(child, sb);
                        break;
                }
            }
        }
    }
}
