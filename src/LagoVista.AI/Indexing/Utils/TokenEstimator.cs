using System;
using System.Text.RegularExpressions;

namespace LagoVista.AI.Indexing.Utils
{
    public static class TokenEstimator
    {
        // Roughly: ASCII ≈ 4 chars per token; CJK ≈ 1 char per token; emoji/URLs/punct inflate counts.
        // This still won't be perfect, but it will UNDERestimate much less.
        private static readonly Regex CjkRegex = new Regex(
            @"[\u3400-\u4DBF\u4E00-\u9FFF\uF900-\uFAFF" +  // CJK Unified
            @"\u3040-\u309F\u30A0-\u30FF" +                // Hiragana/Katakana
            @"\uAC00-\uD7AF" +                             // Hangul
            @"]", RegexOptions.Compiled);


        private static readonly Regex UrlRegex = new Regex(@"https?://\S+|www\.\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PunctRegex = new Regex(@"[^\w\s]", RegexOptions.Compiled);

        public static int EstimateTokens(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;

            int length = s.Length;

            // Count categories
            int cjk = CjkRegex.Matches(s).Count;
            int urls = UrlRegex.Matches(s).Count;
            int punct = PunctRegex.Matches(s).Count;

            // ASCII-ish characters (approx): total minus obvious CJK
            int asciiLike = Math.Max(0, length - cjk);

            // Words (helps when text has many short words)
            int words = Regex.Matches(s, @"\b\w+\b").Count;

            // Base from ASCII text: ceil(chars / 4)
            int baseAsciiTokens = (int)Math.Ceiling(asciiLike / 4.0);

            // CJK: roughly 1 token per char
            int cjkTokens = cjk;


            // URLs often tokenize densely; add ~ (url length / 3) each, but cap minimum of 8 per URL
            int urlTokens = 0;
            foreach (Match m in UrlRegex.Matches(s))
            {
                urlTokens += Math.Max(8, (int)Math.Ceiling(m.Value.Length / 3.0));
            }

            // Punctuation clusters add overhead; ~1 token per 3 punctuation chars
            int punctTokens = (int)Math.Ceiling(punct / 3.0);

            // Also ensure not below ~0.7 * words (short words + spaces tend to increase tokens)
            int wordFloor = (int)Math.Ceiling(words * 0.7);

            int approx = Math.Max(baseAsciiTokens + cjkTokens + urlTokens + punctTokens, wordFloor);

            // Small overhead for BOS/EOS-ish effects/newlines/clusters
            return approx + 2;
        }
    }
}
