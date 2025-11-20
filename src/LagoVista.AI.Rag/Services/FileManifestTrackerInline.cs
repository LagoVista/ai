// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: ba762a3c442c7db2aec2f6d8810ba99c133fe1927150e218a1a4a23ad29937f7
// IndexVersion: 2
// --- END CODE INDEX META ---
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
            @"^//\s---\sBEGIN\sCODE\sINDEX\sMETA\s\(do\snot\sedit\)\s---\s*\r?\n" +
            @"//\sContentHash:\s(?<hash>[0-9a-f]{64})\s*\r?\n" +
            @"//\sIndexVersion:\s(?<ver>\d+)\s*\r?\n" +
            @"//\s---\sEND\sCODE\sINDEX\sMETA\s---\s*\r?\n?",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant);

        /// <summary>
        /// Return C# files that need (re)indexing.
        /// A file is flagged when:
        ///  - it has no header, or
        ///  - the stored hash != current hash of the body (header excluded, CRLF-normalized).
        /// </summary>
        public IEnumerable<string> GetFilesNeedingIndex(IEnumerable<string> csharpFiles, int currentVersion)
        {
            foreach (var file in csharpFiles)
            {
                if (!File.Exists(file)) continue;

                var textRaw = File.ReadAllText(file);
                var hasHeader = TryReadStoredHash(textRaw, out var storedHash, out var version);

                // Compute current body hash on normalized body WITHOUT header
                var bodyText = StripHeader(textRaw, out _);
                var currentHash = ContentHashUtil.ComputeContentHash(bodyText);

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

            // 2) Compute body hash on normalized text
            var hash = ContentHashUtil.ComputeContentHash(bodyOnly);

            // 3) Normalize body to CRLF so subsequent hashing/reads are stable
            var bodyCrlf = ContentHashUtil.NormalizeToCrlf(bodyOnly);

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
