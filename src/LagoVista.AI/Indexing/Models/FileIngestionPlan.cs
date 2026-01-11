using System;
using System.Collections.Generic;

namespace LagoVista.AI.Indexing.Models
{
    /// <summary>
    /// File-level ingestion decision for a single source file.
    /// Canonical version derived from earlier iterations.
    /// </summary>
    public class PlannedFileIngestion
    {
        public string RepoRoot { get; set; }
        public string ProjectId { get; set; }
        public string RepoUrl { get; set; }
        public string CanonicalPath { get; set; }
        public string DocId { get; set; }

        /// <summary>
        /// null | "chunk" | "full" per IDX reindex semantics.
        /// </summary>
        public string Reindex { get; set; }

        /// <summary>
        /// True if this file is considered active (content changed vs last index).
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Optional subkind classification at planning time.
        /// </summary>
        public string SubKind { get; set; }
    }

    /// <summary>
    /// Planned deletion of a document from the index.
    /// </summary>
    public class PlannedDocDeletion
    {
        public string RepoRoot { get; set; }
        public string ProjectId { get; set; }
        public string RepoUrl { get; set; }
        public string CanonicalPath { get; set; }
        public string DocId { get; set; }
    }

    /// <summary>
    /// Overall ingestion plan for a single repository.
    /// This is the canonical FileIngestionPlan contract used by the orchestrator
    /// and indexing pipeline.
    /// </summary>
    public class FileIngestionPlan
    {
        /// <summary>
        /// Absolute path to the repository root on disk.
        /// </summary>
        public string RepoRoot { get; set; }

        /// <summary>
        /// Optional remote URL or logical id for the repository.
        /// </summary>
        public string RepoUrl { get; set; }

        /// <summary>
        /// Branch or ref used when the ingestion plan was computed.
        /// </summary>
        public string BranchRef { get; set; }

        /// <summary>
        /// Project id / tenant scope for this plan.
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Files that should be (re)indexed.
        /// </summary>
        public List<PlannedFileIngestion> FilesToIndex { get; set; } = new List<PlannedFileIngestion>();

        /// <summary>
        /// Documents that should be deleted from the index.
        /// </summary>
        public List<PlannedDocDeletion> DocsToDelete { get; set; } = new List<PlannedDocDeletion>();
    }
}
