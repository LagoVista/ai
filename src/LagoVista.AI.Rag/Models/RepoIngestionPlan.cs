using System;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Models
{
    /// <summary>
    /// Represents the planned indexing and deletion actions for a single repository.
    /// This is a pure data contract consumed by the ingest pipeline and services
    /// that talk to the vector database.
    /// </summary>
    public class RepoIngestionPlan
    {
        /// <summary>
        /// Absolute path to the repository root on disk.
        /// </summary>
        public string RepoRoot { get; set; }

        /// <summary>
        /// Logical remote URL for the repository, typically from GitRepoInspector.
        /// </summary>
        public string RepoUrl { get; set; }

        /// <summary>
        /// Project identifier used for canonical path construction (IDX-003).
        /// Usually the repository name (e.g. "co.core").
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Files that should be (re)indexed in this run.
        /// </summary>
        public List<FileIndexAction> FilesToIndex { get; set; } = new List<FileIndexAction>();

        /// <summary>
        /// Files whose chunks should be deleted from the vector DB because the
        /// file is no longer present or has been filtered out (IDX-0035).
        /// </summary>
        public List<FileDeleteAction> FilesToDelete { get; set; } = new List<FileDeleteAction>();
    }

    /// <summary>
    /// Planned indexing action for a single file.
    /// </summary>
    public class FileIndexAction
    {
        /// <summary>
        /// The key used in the local index (typically repo-relative path).
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Canonical path as defined in IDX-003 (leading slash, projectId as
        /// first segment, forward slashes, lowercase).
        /// </summary>
        public string CanonicalPath { get; set; }

        /// <summary>
        /// Deterministic DocId (UUIDv5) per IDX-001.
        /// </summary>
        public string DocId { get; set; }

        /// <summary>
        /// Normalized repository URL (lowercase, no trailing slash).
        /// </summary>
        public string RepoUrl { get; set; }

        /// <summary>
        /// Project identifier used to build the canonical path.
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Reindex directive: null | "chunk" | "full".
        /// </summary>
        public string ReindexMode { get; set; }

        /// <summary>
        /// True if the file has never been indexed before.
        /// </summary>
        public bool IsNew { get; set; }

        /// <summary>
        /// True if ActiveContentHash != ContentHash in the local index.
        /// </summary>
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Planned deletion action for a single file whose chunks should be removed
    /// from the vector DB (IDX-0034, IDX-0035).
    /// </summary>
    public class FileDeleteAction
    {
        /// <summary>
        /// FilePath key from the local index.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Canonical path for this file (IDX-003).
        /// </summary>
        public string CanonicalPath { get; set; }

        /// <summary>
        /// Deterministic DocId for this file (IDX-001).
        /// </summary>
        public string DocId { get; set; }
    }
}
