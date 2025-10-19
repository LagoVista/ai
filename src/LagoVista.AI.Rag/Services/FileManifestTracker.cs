using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Inline manifest tracker that stores ONLY the content hash in a header comment
    /// at the top of each C# file. It:
    ///   - Computes the hash on the file *without* the header block
    ///   - Normalizes line endings to CRLF (\r\n) for hashing and when writing
    ///   - Inserts or updates the header and rewrites the entire file with CRLF
    ///   - Avoids the "double index" issue after first header insertion
    /// </summary>
    public class FileManifestTrackerInline
    {
        public const string BeginMarker = "// --- BEGIN CODE INDEX META (do not edit) ---";
        public const string EndMarker = "// --- END CODE INDEX META ---";
        //public const int IndexVersion = 1;

        private static readonly Regex HeaderRegex = new Regex(
            @"^\/\/\s---\sBEGIN\sCODE\sINDEX\sMETA\s\(do\snot\sedit\)\s---\s*\r?\n" +
            @"\/\/\sContentHash:\s(?<hash>[0-9a-f]{64})\s*\r?\n" +
            @"\/\/\sIndexVersion:\s(?<ver>\d+)\s*\r?\n" +
            @"\/\/\s---\sEND\sCODE\sINDEX\sMETA\s---\s*\r?\n?",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant);

        /// <summary>
        /// Return C# files that need (re)indexing.
        /// A file is flagged when:
        ///  - it has no header, or
        ///  - the stored hash != current hash of the body (header excluded, CRLF normalized).
        /// </summary>
        public IEnumerable<string> GetFilesNeedingIndex(IEnumerable<string> csharpFiles, int currentVersion)
        {
            foreach (var file in csharpFiles)
            {
                if (!File.Exists(file)) continue;

                var textRaw = File.ReadAllText(file);
                var hasHeader = TryReadStoredHash(textRaw, out var storedHash, out var version);

                // Compute current body hash on CRLF-normalized body WITHOUT header
                var bodyText = StripHeader(textRaw, out _);
                var bodyCrlf = NormalizeToCrlf(bodyText);
                var currentHash = ComputeHash(bodyCrlf);

                if (!hasHeader || !string.Equals(storedHash, currentHash, StringComparison.OrdinalIgnoreCase) || currentVersion != version)
                {
                    yield return file;
                }
            }
        }

        /// <summary>
        /// After successful (re)indexing of a file, write/update the inline header
        /// using the body hash (header excluded), and rewrite the whole file with CRLF line endings.
        /// </summary>
        public void UpsertInlineHeader(string filePath, int version)
        {
            var textRaw = File.ReadAllText(filePath);

            // 1) Remove existing header (if any)
            var bodyOnly = StripHeader(textRaw, out var hadHeader);

            // 2) Normalize body to CRLF so subsequent hashing/reads are stable
            var bodyCrlf = NormalizeToCrlf(bodyOnly);

            // 3) Compute body hash on CRLF text
            var hash = ComputeHash(bodyCrlf);

            // 4) Build header (CRLF newlines)
            var header = BuildHeader(hash, version);

            // 5) Recompose file: header + body (both CRLF)
            var finalText = header + bodyCrlf;

            // 6) Write back (ensures CRLF newlines across entire file)
            File.WriteAllText(filePath, finalText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        /// <summary>
        /// Try to read the stored hash/version from the header.
        /// </summary>
        public static bool TryReadStoredHash(string text, out string storedHash, out int storedVersion)
        {
            storedHash = string.Empty;
            storedVersion = 0;

            var m = HeaderRegex.Match(text);
            if (!m.Success) return false;

            storedHash = m.Groups["hash"].Value;
            _ = int.TryParse(m.Groups["ver"].Value, out storedVersion);
            return true;
        }

        /// <summary>
        /// Remove header block from text if present. Returns the body content.
        /// </summary>
        private static string StripHeader(string text, out bool hadHeader)
        {
            var m = HeaderRegex.Match(text);
            if (m.Success)
            {
                hadHeader = true;
                // Remove the exact header span
                return text.Remove(m.Index, m.Length);
            }
            hadHeader = false;
            return text;
        }

        /// <summary>
        /// Normalize all line endings in the provided text to CRLF (\r\n).
        /// </summary>
        private static string NormalizeToCrlf(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // First, normalize all to LF
            var lf = text.Replace("\r\n", "\n").Replace("\r", "\n");
            // Then convert to CRLF
            return lf.Replace("\n", "\r\n");
        }

        /// <summary>
        /// Compute a SHA-256 hash of the given string (UTF-8 bytes).
        /// </summary>
        private static string ComputeHash(string text)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(text);
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// Build the header string with CRLF line endings.
        /// </summary>
        private static string BuildHeader(string hash, int version)
        {
            var nl = "\r\n";
            var sb = new StringBuilder();
            sb.Append(BeginMarker).Append(nl);
            sb.Append("// ContentHash: ").Append(hash).Append(nl);
            sb.Append("// IndexVersion: ").Append(version).Append(nl);
            sb.Append(EndMarker).Append(nl);
            return sb.ToString();
        }
    }
}