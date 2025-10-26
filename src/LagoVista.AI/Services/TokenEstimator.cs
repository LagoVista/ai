using System;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// Rough token estimator for OpenAI embeddings.
    /// OpenAI tokens are roughly ~4 UTF-8 bytes on average for English and code.
    /// This provides a quick, conservative estimate to prevent oversize embedding requests.
    /// </summary>
    public static class TokenEstimator
    {
        public static int EstimateTokens(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;

            // bytes/4 heuristic for rough estimate
            int bytes = System.Text.Encoding.UTF8.GetByteCount(s);
            int approx = bytes / 4;

            // ensure at least #words count
            int words = s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            approx = Math.Max(approx, words);

            // add small overhead for punctuation/newlines
            return approx + 2;
        }
    }
}
