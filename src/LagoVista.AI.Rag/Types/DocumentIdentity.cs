using System;

namespace LagoVista.AI.Rag.Types
{
    /// <summary>
    /// Document-level identity information for a single source artifact.
    ///
    /// This is the glue between filesystem/Repo details and the logical
    /// identifiers defined in the IDX spec:
    ///   - CanonicalPath / BlobUri (IDX-003)
    ///   - DocId (IDX-001)
    ///
    /// All fields are simple strings to keep JSON serialization and
    /// manual editing straightforward.
    /// </summary>
    public sealed class DocumentIdentity
    {
        /// <summary>
        /// Normalized remote URL for the repository that owns this file,
        /// e.g. https://github.com/org/repo.git (lowercased, no trailing slash).
        /// </summary>
        public string RepoUrl { get; set; }

        /// <summary>
        /// Project identifier used as the first path segment in canonical paths,
        /// typically the repo folder name from your config (e.g. "co.core").
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Logical path of the file within the repo, using forward slashes,
        /// relative to the project root (e.g. "src/Managers/DeviceManager.cs").
        /// This is pre-canonical, preserved mainly for diagnostics.
        /// </summary>
        public string PathInRepo { get; set; }

        /// <summary>
        /// Canonical path computed per IDX-003 (BuildCanonicalPath), e.g.
        ///   "/co.core/src/managers/devicemanager.cs".
        /// </summary>
        public string CanonicalPath { get; set; }

        /// <summary>
        /// Blob URI used when storing the raw text in your content repo.
        /// For V1 we mirror the canonical path (IDX-003 BlobUriMirrorsPath=true).
        /// </summary>
        public string BlobUri { get; set; }

        /// <summary>
        /// Stable document identifier for all chunks originating from this file,
        /// computed via UUIDv5 over &lt;RepoUrl&gt;|&lt;CanonicalPath&gt; and formatted
        /// as 32 uppercase hex characters with no hyphens (IDX-001).
        /// </summary>
        public string DocId { get; set; }
    }

    /// <summary>
    /// Factory helpers for creating <see cref="DocumentIdentity"/> instances.
    /// These are deterministic and side-effect free so they are easy to test.
    /// </summary>
    public static class DocumentIdentityFactory
    {
        /// <summary>
        /// Create a <see cref="DocumentIdentity"/> from repo URL, projectId and
        /// a repo-relative path (e.g. "src/Managers/DeviceManager.cs").
        ///
        /// This applies the canonicalization rules from IDX-001/IDX-003 by
        /// delegating to <see cref="IndexIds"/>.
        /// </summary>
        public static DocumentIdentity Create(string repoUrl, string projectId, string pathInRepo)
        {
            if (string.IsNullOrWhiteSpace(repoUrl)) throw new ArgumentNullException(nameof(repoUrl));
            if (string.IsNullOrWhiteSpace(projectId)) throw new ArgumentNullException(nameof(projectId));
            if (string.IsNullOrWhiteSpace(pathInRepo)) throw new ArgumentNullException(nameof(pathInRepo));

            // Normalize the repo URL and build canonical path first.
            var normalizedRepoUrl = IndexIds.NormalizeRepoUrl(repoUrl);

            // Ensure forward slashes and no leading slash before canonicalization.
            var cleanedPathInRepo = pathInRepo
                .Replace('\\', '/')
                .Trim();

            // Canonical path (includes projectId as first segment, leading slash,
            // forward slashes, lowercase).
            var canonicalPath = IndexIds.BuildCanonicalPath(projectId, cleanedPathInRepo);

            // DocId computed from normalized repo URL and canonical path.
            var docId = IndexIds.ComputeDocId(normalizedRepoUrl, canonicalPath);

            // For V1 we keep BlobUri mirrored to canonical path per IDX-003.
            var blobUri = canonicalPath;

            return new DocumentIdentity
            {
                RepoUrl = normalizedRepoUrl,
                ProjectId = projectId,
                PathInRepo = cleanedPathInRepo,
                CanonicalPath = canonicalPath,
                BlobUri = blobUri,
                DocId = docId
            };
        }
    }
}
