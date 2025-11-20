using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Helpers for computing normalized SHA-256 content hashes.
    ///
    /// Implements IDX-014 / IDX-016 / IDX-020 semantics:
    ///   - Normalize line endings.
    ///   - Truncate any source line over 500 characters.
    ///   - Compute SHA-256 over the normalized text.
    ///
    /// This is used both for file-level change detection in the local index
    /// and for per-chunk ContentHash values.
    /// </summary>
    public static class ContentHashHelper
    {
        /// <summary>
        /// Compute a normalized SHA-256 hash for an in-memory text value.
        ///
        /// Rules:
        ///  - Normalize all line endings to '\n'.
        ///  - Truncate any individual line to max 500 characters.
        ///  - Join lines with '\n' and hash UTF-8 bytes.
        ///  - Return 64-char lowercase hex string.
        /// </summary>
        public static string ComputeHash(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            // Normalize to LF first.
            var lf = text.Replace("\r\n", "\n").Replace("\r", "\n");

            var sb = new StringBuilder(lf.Length);
            using (var reader = new StringReader(lf))
            {
                string line;
                bool first = true;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!first)
                        sb.Append('\n');
                    else
                        first = false;

                    if (line.Length > 500)
                        line = line.Substring(0, 500);

                    sb.Append(line);
                }
            }

            var normalized = sb.ToString();
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(normalized);
                var hash = sha.ComputeHash(bytes);
                var hex = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        /// <summary>
        /// Convenience helper: read the file as text and compute its normalized hash.
        /// </summary>
        public static string ComputeFileHash(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var text = File.ReadAllText(filePath, Encoding.UTF8);
            return ComputeHash(text);
        }
    }
}
