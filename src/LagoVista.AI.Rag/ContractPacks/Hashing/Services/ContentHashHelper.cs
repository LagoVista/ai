using System;
using System.Security.Cryptography;
using System.Text;

namespace LagoVista.AI.Rag.ContractPacks.Hashing.Services
{
    /// <summary>
    /// Canonical content hashing implementation.
    /// Normalizes line endings to LF and truncates lines to 500 characters
    /// before computing the hash (per IDX specs).
    /// </summary>
    public static class ContentHashHelper
    {
        public static string GetHashForText(string text)
        {
            if (text == null) return string.Empty;

            var normalized = Normalize(text);
            return ComputeHash(normalized);
        }

        private static string Normalize(string input)
        {
            // Normalize line endings
            var lf = input.Replace("\r\n", "\n").Replace("\r", "\n");

            var sb = new StringBuilder();
            var lines = lf.Split('\n');

            foreach (var line in lines)
            {
                if (line.Length > 500)
                {
                    sb.Append(line.Substring(0, 500));
                }
                else
                {
                    sb.Append(line);
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }

        private static string ComputeHash(string input)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
