using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Local index store for tracking per-file indexing state.
    /// Implements IDX-0036: local-index.json with one record per file.
    ///
    /// Responsibilities:
    /// - Load/save JSON index file.
    /// - Track ContentHash (last indexed) and ActiveContentHash (current on-disk).
    /// - Support detection of missing files and active files.
    /// - Persist SubKind overrides and Reindex flags.
    /// - Persist per-file facet metadata snapshot for crash-safe MetadataRegistry recovery.
    /// </summary>
    public class LocalIndexRecord
    {
        public string FilePath { get; set; }

        public string DocId { get; set; }

        public string ContentHash { get; set; }

        public string ActiveContentHash { get; set; }

        public string SubKind { get; set; }

        public DateTime? LastIndexedUtc { get; set; }

        public bool? FlagForReview { get; set; }

        /// <summary>
        /// null | "chunk" | "full" per IDX-0036.
        /// </summary>
        public string Reindex { get; set; }

        /// <summary>
        /// Snapshot of facet values discovered for this file during the last successful index.
        ///
        /// We store these so that, if an indexing run fails mid-stream, the next run can
        /// reconstruct a complete MetadataRegistryReport directly from local-index.json
        /// without re-scanning already-processed files.
        /// </summary>
        public List<FacetValue> Facets { get; set; } = new List<FacetValue>();

        /// <summary>
        /// True when the current on-disk content differs from the last indexed content.
        /// ActiveContentHash and ContentHash must both be non-empty.
        /// </summary>
        public bool IsActive
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ActiveContentHash) || string.IsNullOrWhiteSpace(ContentHash))
                    return false;

                return !string.Equals(ActiveContentHash, ContentHash, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    internal class LocalIndexFile
    {
        public List<LocalIndexRecord> Records { get; set; } = new List<LocalIndexRecord>();
    }

    /// <summary>
    /// Manages loading, updating, and saving the local-index.json file.
    ///
    /// Call pattern for the ingestor (per repo):
    /// - var store = LocalIndexStore.Load(indexPath);
    /// - For each discovered file, compute hash and call UpdateActiveContentHash().
    /// - Use GetMissingFiles() to find records whose files no longer exist.
    /// - After successfully indexing a file, call MarkIndexed().
    /// - Call Save() periodically or after each file for crash-safe behavior.
    /// </summary>
    public class LocalIndexStore
    {
        private readonly string _indexFilePath;
        private readonly Dictionary<string, LocalIndexRecord> _records;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public LocalIndexStore(string indexFilePath)
        {
            if (string.IsNullOrWhiteSpace(indexFilePath))
                throw new ArgumentNullException(nameof(indexFilePath));

            _indexFilePath = indexFilePath;
            _records = new Dictionary<string, LocalIndexRecord>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Load the store from disk if the file exists; otherwise returns an empty store.
        /// Corrupt files are renamed with a .corrupt-YYYYMMDDHHMMSS suffix.
        /// </summary>
        public static LocalIndexStore Load(string indexFilePath)
        {
            var store = new LocalIndexStore(indexFilePath);
            store.LoadFromDisk();
            return store;
        }

        /// <summary>
        /// All records currently in the store.
        /// </summary>
        public IEnumerable<LocalIndexRecord> Records
        {
            get { return _records.Values; }
        }

        /// <summary>
        /// Get an existing record by path or create a new one with just FilePath populated.
        /// FilePath should be the canonical path used consistently for this repo.
        /// </summary>
        public LocalIndexRecord GetOrAdd(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            if (!_records.TryGetValue(filePath, out var record))
            {
                record = new LocalIndexRecord
                {
                    FilePath = filePath
                };

                _records[filePath] = record;
            }

            return record;
        }

        /// <summary>
        /// Remove a record for the given file path, if present.
        /// </summary>
        public void Remove(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            _records.Remove(filePath);
        }

        /// <summary>
        /// Returns records whose FilePath is not present in the currentFilePaths set.
        /// Call this after discovering all files on disk to determine missing/orphaned entries.
        /// </summary>
        public IEnumerable<LocalIndexRecord> GetMissingFiles(IEnumerable<string> currentFilePaths)
        {
            var current = new HashSet<string>(currentFilePaths ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            foreach (var record in _records.Values)
            {
                if (!current.Contains(record.FilePath))
                    yield return record;
            }
        }

        /// <summary>
        /// Update the ActiveContentHash for a file. Call this with the hash of the current
        /// on-disk content (normalized) before deciding whether to reindex.
        /// </summary>
        public void UpdateActiveContentHash(string filePath, string activeContentHash)
        {
            var record = GetOrAdd(filePath);
            record.ActiveContentHash = activeContentHash;
        }

        /// <summary>
        /// Mark a file as successfully indexed. Sets ContentHash and ActiveContentHash
        /// to the same value, updates LastIndexedUtc, clears the Reindex flag,
        /// and optionally captures a snapshot of facet metadata.
        /// </summary>
        public void MarkIndexed(string filePath, string contentHash, DateTime indexedUtc, string subKind = null, List<FacetValue> facets = null)
        {
            var record = GetOrAdd(filePath);
            record.ContentHash = contentHash;
            record.ActiveContentHash = contentHash;
            record.LastIndexedUtc = indexedUtc;

            if (!string.IsNullOrWhiteSpace(subKind) && string.IsNullOrWhiteSpace(record.SubKind))
            {
                record.SubKind = subKind;
            }

            if (facets != null && facets.Count > 0)
            {
                record.Facets = MergeFacets(record.Facets, facets);
            }

            record.Reindex = null;
        }

        /// <summary>
        /// Set the Reindex directive: null | "chunk" | "full".
        /// This persists across runs until cleared by MarkIndexed().
        /// </summary>
        public void SetReindex(string filePath, string reindexMode)
        {
            if (reindexMode != null && reindexMode != "chunk" && reindexMode != "full")
                throw new ArgumentOutOfRangeException(nameof(reindexMode), "Reindex must be null, 'chunk', or 'full'.");

            var record = GetOrAdd(filePath);
            record.Reindex = reindexMode;
        }

        /// <summary>
        /// Persist the current index to disk using an atomic write (temp file + move).
        /// Records are sorted by FilePath ascending.
        /// </summary>
        public void Save()
        {
            var directory = Path.GetDirectoryName(_indexFilePath);
            if (string.IsNullOrWhiteSpace(directory))
                directory = ".";

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var file = new LocalIndexFile
            {
                Records = _records.Values
                    .OrderBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            var json = JsonSerializer.Serialize(file, JsonOptions);
            var tempPath = _indexFilePath + ".tmp";

            File.WriteAllText(tempPath, json);

            if (File.Exists(_indexFilePath))
            {
                File.Delete(_indexFilePath);
            }

            File.Move(tempPath, _indexFilePath);
        }

        /// <summary>
        /// Compute the default index path for a repo root, per IDX-0036:
        /// &lt;repoRoot&gt;/.nuvos/index/local-index.json
        /// </summary>
        public static string GetDefaultIndexPath(string repoRoot)
        {
            if (string.IsNullOrWhiteSpace(repoRoot))
                throw new ArgumentNullException(nameof(repoRoot));

            return Path.Combine(repoRoot, ".nuvos", "index", "local-index.json");
        }

        private void LoadFromDisk()
        {
            if (!File.Exists(_indexFilePath))
                return;

            try
            {
                var json = File.ReadAllText(_indexFilePath);
                if (string.IsNullOrWhiteSpace(json))
                    return;

                var file = JsonSerializer.Deserialize<LocalIndexFile>(json, JsonOptions);
                if (file == null || file.Records == null)
                    return;

                _records.Clear();
                foreach (var record in file.Records)
                {
                    if (record != null && !string.IsNullOrWhiteSpace(record.FilePath))
                    {
                        if (record.Facets == null)
                        {
                            record.Facets = new List<FacetValue>();
                        }

                        _records[record.FilePath] = record;
                    }
                }
            }
            catch
            {
                // If the index is corrupt, rename it and start with a fresh, empty store.
                try
                {
                    var directory = Path.GetDirectoryName(_indexFilePath) ?? ".";
                    var fileName = Path.GetFileNameWithoutExtension(_indexFilePath);
                    var ext = Path.GetExtension(_indexFilePath);
                    var backupName = fileName + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ext;
                    var backupPath = Path.Combine(directory, backupName);
                    File.Move(_indexFilePath, backupPath);
                }
                catch
                {
                    // Swallow all errors here; worst-case we lose the old index file.
                }
            }
        }

        private static List<FacetValue> MergeFacets(List<FacetValue> existing, IEnumerable<FacetValue> incoming)
        {
            var result = new List<FacetValue>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (existing != null)
            {
                foreach (var f in existing)
                {
                    if (f == null || string.IsNullOrWhiteSpace(f.Property) || string.IsNullOrWhiteSpace(f.Value))
                        continue;

                    var key = f.Property + "|" + f.Value;
                    if (seen.Add(key))
                    {
                        result.Add(new FacetValue
                        {
                            Property = f.Property,
                            Value = f.Value
                        });
                    }
                }
            }

            if (incoming != null)
            {
                foreach (var f in incoming)
                {
                    if (f == null || string.IsNullOrWhiteSpace(f.Property) || string.IsNullOrWhiteSpace(f.Value))
                        continue;

                    var key = f.Property + "|" + f.Value;
                    if (seen.Add(key))
                    {
                        result.Add(new FacetValue
                        {
                            Property = f.Property,
                            Value = f.Value
                        });
                    }
                }
            }

            return result;
        }
    }
}
