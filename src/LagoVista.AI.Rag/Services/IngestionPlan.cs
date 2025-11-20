using System;
using System.Collections.Generic;
using System.Linq;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Represents the planned work for a single file in an ingestion run.
    /// </summary>
    public class PlannedFile
    {
        public string FilePath { get; set; }

        public LocalIndexRecord Record { get; set; }

        /// <summary>
        /// True when this file should be (re)indexed in the current run.
        /// </summary>
        public bool NeedsIndex { get; set; }

        /// <summary>
        /// True when this file no longer exists on disk and its chunks should be deleted.
        /// </summary>
        public bool IsMissing { get; set; }

        /// <summary>
        /// True when Reindex is explicitly set to "chunk" or "full".
        /// </summary>
        public bool ForceReindex { get; set; }
    }

    /// <summary>
    /// Aggregated ingestion plan for a repository.
    /// </summary>
    public class IngestionPlan
    {
        private readonly List<PlannedFile> _files;

        public IngestionPlan(List<PlannedFile> files)
        {
            _files = files ?? new List<PlannedFile>();
        }

        /// <summary>
        /// All files known to the planner (existing and missing).
        /// </summary>
        public IReadOnlyList<PlannedFile> Files
        {
            get { return _files; }
        }

        /// <summary>
        /// Files that need (re)indexing and still exist on disk.
        /// </summary>
        public IReadOnlyList<PlannedFile> FilesToIndex
        {
            get { return _files.Where(f => f.NeedsIndex && !f.IsMissing).ToList(); }
        }

        /// <summary>
        /// Files that are present in the local index but missing from the filesystem.
        /// </summary>
        public IReadOnlyList<PlannedFile> MissingFiles
        {
            get { return _files.Where(f => f.IsMissing).ToList(); }
        }

        public bool HasWork
        {
            get { return FilesToIndex.Count > 0 || MissingFiles.Count > 0; }
        }
    }
}
