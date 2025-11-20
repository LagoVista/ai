using System;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Types
{
    /// <summary>
    /// File-level ingestion decision for a single source file.
    /// </summary>
    public class PlannedFileIngestion
    {
        public string RepoRoot { get; set; }
        public string ProjectId { get; set; }
        public string RepoUrl { get; set; }
        public string CanonicalPath { get; set; }
        public string DocId { get; set; }
        public string LocalPath { get; set; }

        /// <summary>
        /// null | "chunk" | "full" per IDX-0036.
        /// </summary>
        public string Reindex { get; set; }

        /// <summary>
        /// True when the current on-disk content differs from the last indexed content
        /// or an explicit Reindex flag is set.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// True when this file has never been indexed before (no ContentHash recorded).
        /// </summary>
        public bool IsNewDocument { get; set; }
    }

    /// <summary>
    /// Planned deletion of all chunks for a DocId (IDX-0034, IDX-0035).
    /// </summary>
    public class PlannedDocDeletion
    {
        public string RepoUrl { get; set; }
        public string DocId { get; set; }
        public string CanonicalPath { get; set; }
    }

    /// <summary>
    /// Overall ingestion plan for a single repository.
    /// </summary>
    public class FileIngestionPlan
    {
        public string RepoRoot { get; set; }
        public string RepoUrl { get; set; }
        public string BranchRef { get; set; }
        public string ProjectId { get; set; }

        public List<PlannedFileIngestion> FilesToIndex { get; set; } = new List<PlannedFileIngestion>();
        public List<PlannedDocDeletion> DocsToDelete { get; set; } = new List<PlannedDocDeletion>();
    }
}
