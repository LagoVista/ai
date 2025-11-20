using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LagoVista.AI.Rag.Types
{
    /// <summary>
    /// Utilities for canonical path construction and DocId / PointId generation.
    ///
    /// Implements:
    ///   - IDX-001 DocId Generation Strategy
    ///   - IDX-002 PointId Generation Strategy
    ///   - IDX-003 Canonical Path & BlobUri Normalization Rules
    ///
    /// These helpers are deterministic and side-effect free so they can be safely
    /// used across the ingestion pipeline.
    /// </summary>
    public static class IndexIds
    {
        // Single fixed namespace GUID for code files (IDX-001.NamespaceGuidVersioning)
        // This must remain stable once chosen.
        private static readonly Guid NamespaceCodeFiles = new Guid("9a9a77a0-6f19-4b44-a8a7-4e4e1a4f3d01");

        /// <summary>
        /// Build the canonical normalized path for a file, using the rules from IDX-003:
        ///   - FirstPathSegment = projectId
        ///   - Use forward slashes
        ///   - Collapse repeated slashes
        ///   - Ensure leading slash
        ///   - Lowercase
        ///
        /// Example: projectId="co.core", pathInRepo="src/Managers/DeviceManager.cs" =>
        ///   "/co.core/src/managers/devicemanager.cs"
        /// </summary>
        public static string BuildCanonicalPath(string projectId, string pathInRepo)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentNullException(nameof(projectId));

            projectId = projectId.Trim().Trim('/', '\\');

            var path = pathInRepo ?? string.Empty;
            // Normalize separators to forward slashes
            path = path.Replace('\\', '/');

            // Collapse repeated slashes
            path = Regex.Replace(path, "/{2,}", "/");

            // Trim leading/trailing slashes before we prepend projectId
            path = path.Trim('/');

            // Combine project and path
            string combined = string.IsNullOrEmpty(path)
                ? projectId
                : projectId + "/" + path;

            // Ensure leading slash
            if (!combined.StartsWith("/", StringComparison.Ordinal))
            {
                combined = "/" + combined;
            }

            // Lowercase for stability
            combined = combined.ToLowerInvariant();

            return combined;
        }

        /// <summary>
        /// Normalize a repository URL according to IDX-001 / IDX-003:
        ///   - Trim whitespace
        ///   - Lowercase
        ///   - Remove trailing slash
        /// </summary>
        public static string NormalizeRepoUrl(string repoUrl)
        {
            if (string.IsNullOrWhiteSpace(repoUrl))
                throw new ArgumentNullException(nameof(repoUrl));

            var url = repoUrl.Trim();

            // Git remotes are slash-based; normalize trailing slash only.
            while (url.EndsWith("/", StringComparison.Ordinal))
            {
                url = url.Substring(0, url.Length - 1);
            }

            return url.ToLowerInvariant();
        }

        /// <summary>
        /// Compute the canonical string used for DocId generation:
        ///   "&lt;RepoUrl&gt;|&lt;CanonicalPath&gt;".
        ///
        /// RepoUrl must already be a logical identifier for the repository (typically
        /// the normalized remote URL from GitRepoInspector).
        /// </summary>
        public static string BuildCanonicalString(string repoUrl, string canonicalPath)
        {
            if (string.IsNullOrWhiteSpace(repoUrl))
                throw new ArgumentNullException(nameof(repoUrl));

            if (string.IsNullOrWhiteSpace(canonicalPath))
                throw new ArgumentNullException(nameof(canonicalPath));

            return repoUrl + "|" + canonicalPath;
        }

        /// <summary>
        /// Compute the DocId from repo URL and canonical path using UUIDv5 semantics
        /// (SHA-1 over NamespaceCodeFiles + canonical string). The result is formatted
        /// as 32 uppercase hex characters with no hyphens, per IDX-001.
        /// </summary>
        public static string ComputeDocId(string repoUrl, string canonicalPath)
        {
            var normalizedRepo = NormalizeRepoUrl(repoUrl);
            var canonical = BuildCanonicalString(normalizedRepo, canonicalPath);

            var guid = CreateUuidV5(NamespaceCodeFiles, canonical);
            return guid.ToString("N").ToUpperInvariant();
        }

        /// <summary>
        /// Convenience helper: build canonical path from projectId + pathInRepo and then
        /// compute DocId.
        /// </summary>
        public static string ComputeDocId(string repoUrl, string projectId, string pathInRepo)
        {
            var canonicalPath = BuildCanonicalPath(projectId, pathInRepo);
            return ComputeDocId(repoUrl, canonicalPath);
        }

        /// <summary>
        /// Generate a new PointId GUID string for a vector chunk (IDX-002).
        /// We keep the standard hyphenated GUID representation for readability.
        /// </summary>
        public static string NewPointId()
        {
            return Guid.NewGuid().ToString("D");
        }

        /// <summary>
        /// Create a UUIDv5 (name-based, SHA-1) from a namespace GUID and a UTF-8 name.
        /// Implementation follows RFC 4122.
        /// </summary>
        private static Guid CreateUuidV5(Guid namespaceId, string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            // Convert namespace UUID to network order (big endian)
            var namespaceBytes = namespaceId.ToByteArray();
            SwapByteOrder(namespaceBytes);

            var nameBytes = Encoding.UTF8.GetBytes(name);

            var data = new byte[namespaceBytes.Length + nameBytes.Length];
            Buffer.BlockCopy(namespaceBytes, 0, data, 0, namespaceBytes.Length);
            Buffer.BlockCopy(nameBytes, 0, data, namespaceBytes.Length, nameBytes.Length);

            byte[] hash;
            using (var sha1 = SHA1.Create())
            {
                hash = sha1.ComputeHash(data);
            }

            var newGuid = new byte[16];
            Array.Copy(hash, 0, newGuid, 0, 16);

            // Set version (5)
            newGuid[6] = (byte)((newGuid[6] & 0x0F) | (5 << 4));
            // Set variant (RFC 4122)
            newGuid[8] = (byte)((newGuid[8] & 0x3F) | 0x80);

            SwapByteOrder(newGuid);

            return new Guid(newGuid);
        }

        /// <summary>
        /// Convert GUID bytes between little-endian (System.Guid internal) and
        /// network order as used by the UUID RFC.
        /// </summary>
        private static void SwapByteOrder(byte[] guid)
        {
            void Swap(int a, int b)
            {
                var tmp = guid[a];
                guid[a] = guid[b];
                guid[b] = tmp;
            }

            Swap(0, 3);
            Swap(1, 2);
            Swap(4, 5);
            Swap(6, 7);
        }
    }
}
