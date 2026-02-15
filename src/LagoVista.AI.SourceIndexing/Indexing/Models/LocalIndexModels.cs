using System;
using System.Collections.Generic;

namespace LagoVista.AI.Indexing.Models
{
    /// <summary>
    /// Canonical per-file local index record (IDX-0036).
    /// This merges the shape used in earlier LocalIndexRecord definitions
    /// and is the single source of truth going forward.
    /// </summary>
    public sealed class LocalIndexRecord
    {
        /// <summary>
        /// Canonical file path (usually relative to project or repo root).
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Deterministic document identifier (IDX-001).
        /// </summary>
        public string DocId { get; set; } = string.Empty;

        /// <summary>
        /// Output hash from the last successful indexing run.
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>
        /// Current on-disk content hash.
        /// </summary>
        public string ActiveContentHash { get; set; } = string.Empty;

        /// <summary>
        /// Optional subkind classification (e.g., Model, Manager, Domain, etc.).
        /// </summary>
        public string SubKind { get; set; }

        /// <summary>
        /// Last time this file was successfully indexed (UTC).
        /// Nullable for legacy or unindexed entries.
        /// </summary>
        public DateTime? LastIndexedUtc { get; set; }

        /// <summary>
        /// Manual review flag; null means no explicit flag has been set.
        /// </summary>
        public bool? FlagForReview { get; set; }

        /// <summary>
        /// null | "chunk" | "full" per IDX-0036.
        /// Controls reindex behavior for this file.
        /// </summary>
        public string Reindex { get; set; }

        /// <summary>
        /// Snapshot of facet values discovered for this file during the
        /// last successful index. Used to rebuild metadata registry state
        /// without reprocessing already-indexed files after a crash.
        /// </summary>
        public List<FacetValue> Facets { get; set; } = new List<FacetValue>();

        /// <summary>
        /// True when current on-disk content differs from last indexed content.
        /// Both ContentHash and ActiveContentHash must be non-empty.
        /// </summary>
        public bool IsActive
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ContentHash) || string.IsNullOrWhiteSpace(ActiveContentHash))
                    return false;

                return !string.Equals(ContentHash, ActiveContentHash, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// Canonical in-memory store for local index records.
    /// This is a pure data/behavior object; file I/O is handled by
    /// ILocalIndexStore implementations.
    /// </summary>
    public sealed class LocalIndexStore
    {
        /// <summary>
        /// Optional logical repository identifier.
        /// </summary>
        public string RepoId { get; set; }

        /// <summary>
        /// Optional project root used when building canonical file paths.
        /// </summary>
        public string ProjectRoot { get; set; }

        /// <summary>
        /// Records keyed by canonical file path.
        /// </summary>
        public Dictionary<string, LocalIndexRecord> Records { get; set; } =
            new Dictionary<string, LocalIndexRecord>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Number of records in the store.
        /// </summary>
        public int Count => Records?.Count ?? 0;

        /// <summary>
        /// Enumerate all records in the store.
        /// </summary>
        public IEnumerable<LocalIndexRecord> GetAll()
        {
            if (Records == null)
                return new List<LocalIndexRecord>();

            return Records.Values;
        }


        /// <summary>
        /// Try to get a record by file path (normalized internally).
        /// </summary>
        public bool TryGet(string filePath, out LocalIndexRecord record)
        {
            if (Records == null)
            {
                record = null;
                return false;
            }

            var key = NormalizePath(filePath);
            return Records.TryGetValue(key, out record);
        }

        /// <summary>
        /// Get an existing record or create a new one initialized with
        /// the provided file path and doc id.
        /// </summary>
        public LocalIndexRecord GetOrAdd(string filePath, string docId)
        {
            if (Records == null)
            {
                Records = new Dictionary<string, LocalIndexRecord>(StringComparer.OrdinalIgnoreCase);
            }

            var key = NormalizePath(filePath);
            if (!Records.TryGetValue(key, out var record))
            {
                record = new LocalIndexRecord
                {
                    FilePath = key,
                    DocId = docId
                };

                Records[key] = record;
            }

            return record;
        }

        /// <summary>
        /// Remove a record by file path.
        /// </summary>
        public bool Remove(string filePath)
        {
            if (Records == null)
                return false;

            var key = NormalizePath(filePath);
            return Records.Remove(key);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            var p = path.Replace('\\', '/');

            while (p.Contains("//"))
            {
                p = p.Replace("//", "/");
            }

            if (p.StartsWith("./", StringComparison.Ordinal))
            {
                p = p.Substring(2);
            }

            return p.ToLowerInvariant();
        }
    }
}
