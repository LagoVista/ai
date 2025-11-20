using System;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Types
{
    /// <summary>
    /// Represents the indexing decision for a single file.
    /// </summary>
    public class FilePlanItem
    {
        public string FilePath { get; set; }

        /// <summary>
        /// Canonical path for the file (usually the same as FilePath for now).
        /// </summary>
        public string CanonicalPath { get; set; }

        /// <summary>
        /// Optional DocId associated with this file when known.
        /// </summary>
        public string DocId { get; set; }

        /// <summary>
        /// Action for this file: "Skip" | "Index" | "Delete".
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Reindex mode: null | "chunk" | "full".
        /// Mirrors LocalIndexRecord.Reindex.
        /// </summary>
        public string ReindexMode { get; set; }

        /// <summary>
        /// Human-friendly reason for the decision (for logs and debugging).
        /// </summary>
        public string Reason { get; set; }
    }

    /// <summary>
    /// Overall plan for a repository indexing run.
    /// </summary>
    public class IngestionPlan
    {
        public List<FilePlanItem> Files { get; set; } = new List<FilePlanItem>();

        /// <summary>
        /// Total number of files discovered on disk for this repo.
        /// </summary>
        public int TotalDiscoveredFiles { get; set; }

        /// <summary>
        /// Records whose files are missing from disk and should have their chunks deleted.
        /// </summary>
        public int TotalMissingFiles { get; set; }

        /// <summary>
        /// How many files should be (re)indexed this run.
        /// </summary>
        public int TotalToIndex { get; set; }

        /// <summary>
        /// How many records are purely deletions (no replacement chunks).
        /// </summary>
        public int TotalToDelete { get; set; }
    }
}
