using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;


namespace LagoVista.AI.Rag.Services
{
    public static class FileWalker
    {
        /// <summary>
        /// Enumerate files under <paramref name="root"/> matching include globs and excluding exclude globs.
        /// Globs are evaluated on the *relative* path using forward slashes, e.g. "src/**/*.cs".
        /// Supported wildcards: *, ?, ** (directory recursive).
        /// </summary>
        public static IEnumerable<string> EnumerateFiles(
        string root,
        IEnumerable<string> includeGlobs,
        IEnumerable<string> excludeGlobs)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                yield break;


            var include = (includeGlobs ?? Array.Empty<string>()).Select(ToRegex).ToArray();
            var exclude = (excludeGlobs ?? Array.Empty<string>()).Select(ToRegex).ToArray();


            // If no include globs provided, default to "**/*" (everything)
            if (include.Length == 0)
                include = new[] { ToRegex("**/*") };


            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(root, file);
                string relNorm = Normalize(rel);


                bool isIncluded = include.Any(rx => rx.IsMatch(relNorm));
                if (!isIncluded) continue;


                bool isExcluded = exclude.Any(rx => rx.IsMatch(relNorm));
                if (isExcluded) continue;


                // Skip obvious binary files by extension (can be extended)
                if (IsLikelyBinary(relNorm)) continue;


                yield return file;
            }
        }


        private static string Normalize(string path)
        {
            // Normalize to forward slashes for glob regex
            return path.Replace(@"/",@"\");
        }


        private static Regex ToRegex(string glob)
        {
            // Normalize & trim leading './'
            var g = Normalize(glob).TrimStart('/');


            // Escape regex special chars first
            string rx = Regex.Escape(g);


            // Bring back glob magic: **, *, ?
            // Replace \*\*\/ -> (.+\/)? to cover directory-recursive segments
            rx = rx.Replace(@"\*\*/", @"(.+/)?");
            // Replace \*\* -> .* (across path segments)
            rx = rx.Replace(@"\*\*", @".*");
            // Replace \* -> [^/]* (within a segment)
            rx = rx.Replace(@"\*", @"[^/]*");
            // Replace \? -> [^/]
            rx = rx.Replace(@"\?", @"[^/]");


            // Anchored match on entire relative path
            return new Regex("^" + rx + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }


        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico",
    ".pdf", ".zip", ".gz", ".tgz", ".7z", ".rar",
    ".dll", ".exe", ".so", ".dylib", ".a", ".lib",
    ".mp3", ".mp4", ".mov", ".wav", ".avi", ".mkv",
    ".woff", ".woff2", ".eot", ".ttf", ".otf"
};

        private static bool IsLikelyBinary(string extension) => AllowedExtensions.Contains(extension);
    }
}