using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Services
{
    public static class ContentHashUtil
    {
        // Normalize all line endings in the provided text to CRLF.
        public static string NormalizeToCrlf(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            if (text.Length == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(text.Length + 16);

            bool lastWasCr = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == (char)13) // CR
                {
                    sb.Append((char)13);
                    sb.Append((char)10);
                    lastWasCr = true;
                    continue;
                }

                if (c == (char)10) // LF
                {
                    if (!lastWasCr)
                    {
                        sb.Append((char)13);
                        sb.Append((char)10);
                    }

                    lastWasCr = false;
                    continue;
                }

                sb.Append(c);
                lastWasCr = false;
            }

            return sb.ToString();
        }

        // Compute a SHA-256 hash of normalized text content.
        // Returns a 64-character lowercase hex string.
        public static string ComputeContentHash(string normalizedText)
        {
            var text = normalizedText ?? string.Empty;

            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }

        // Compute the content hash for a file by:
        //  - reading the file as UTF-8 (with BOM detection),
        //  - normalizing line endings to CRLF,
        //  - hashing the normalized text with SHA-256.
        public static async Task<string> ComputeFileContentHashAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            using (var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                useAsync: true))
            using (var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true))
            {
                var content = await reader.ReadToEndAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                var normalized = NormalizeToCrlf(content);
                return ComputeContentHash(normalized);
            }
        }
    }
}