using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LagoVista.AI.Rag.Types
{
    /// <summary>
    /// Represents a single file entry in the local index.
    ///
    /// Contract derived from IDX-0036:
    ///   - FilePath: canonical path (relative to project root)
    ///   - DocId: stable document identifier (IDX-001)
    ///   - ContentHash: hash of the last content successfully indexed
    ///   - ActiveContentHash: hash of the current on-disk content
    ///   - SubKind: optional override / classification
    ///   - LastIndexedUtc: when the content was last successfully indexed
    ///   - FlagForReview: manual review flag
    ///   - Reindex: null | "chunk" | "full"
    /// </summary>
    public sealed class LocalIndexRecord
    {
        public string FilePath { get; set; } = string.Empty;

        public string DocId { get; set; } = string.Empty;

        public string ContentHash { get; set; } = string.Empty;

        public string ActiveContentHash { get; set; } = string.Empty;

        public string SubKind { get; set; }

        public DateTime LastIndexedUtc { get; set; }

        public bool? FlagForReview { get; set; }

        /// <summary>
        /// null | "chunk" | "full"
        /// </summary>
        public string Reindex { get; set; }
    }

    /// <summary>
    /// Local index file manager (IDX-0036).
    ///
    /// Responsibilities:
    ///   - Load existing index from <project-root>/.nuvos/index/local-index.json
    ///   - Tolerate missing or corrupt files (start fresh, rename corrupt)
    ///   - Maintain an in-memory dictionary keyed by FilePath
    ///   - Persist in a crash-safe / atomic way after updates
    ///   - Always write entries sorted by FilePath ascending
    ///
    /// This class does NOT compute hashes by itself; callers are responsible for
    /// computing ContentHash and ActiveContentHash according to IDX-016/IDX-0036.
    /// </summary>
    public sealed class LocalIndexStore
    {
        private const string IndexFolder = ".nuvos/index";
        private const string IndexFileName = "local-index.json";

        private readonly Dictionary<string, LocalIndexRecord> _records;

        public string ProjectRoot { get; }

        [JsonIgnore]
        public string IndexFilePath => Path.Combine(ProjectRoot, IndexFolder, IndexFileName);

        private LocalIndexStore(string projectRoot, Dictionary<string, LocalIndexRecord> records)
        {
            ProjectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
            _records = records ?? new Dictionary<string, LocalIndexRecord>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Load the local index for the given project root.
        ///
        /// Startup rules (IDX-0036):
        ///   - If file exists and is valid JSON, load it.
        ///   - If file is corrupt, rename it with a .corrupt timestamp suffix and start empty.
        ///   - If file is missing, start empty.
        /// </summary>
        public static LocalIndexStore Load(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentNullException(nameof(projectRoot));

            projectRoot = Path.GetFullPath(projectRoot);

            var records = new Dictionary<string, LocalIndexRecord>(StringComparer.OrdinalIgnoreCase);

            var folder = Path.Combine(projectRoot, IndexFolder);
            var filePath = Path.Combine(folder, IndexFileName);

            if (File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath, Encoding.UTF8);
                    var list = JsonSerializer.Deserialize<List<LocalIndexRecord>>(json) ?? new List<LocalIndexRecord>();

                    foreach (var rec in list)
                    {
                        if (rec == null || string.IsNullOrWhiteSpace(rec.FilePath))
                            continue;

                        var key = NormalizePath(rec.FilePath);
                        rec.FilePath = key;
                        if (!records.ContainsKey(key))
                        {
                            records[key] = rec;
                        }
                    }
                }
                catch
                {
                    // Corrupt file; rename and start fresh.
                    try
                    {
                        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                        var corruptName = Path.Combine(folder, $"{IndexFileName}.{stamp}.corrupt");
                        Directory.CreateDirectory(folder);
                        File.Move(filePath, corruptName, overwrite: true);
                    }
                    catch
                    {
                        // Best effort only; ignore failures here.
                    }
                }
            }

            return new LocalIndexStore(projectRoot, records);
        }

        /// <summary>
        /// Enumerate records in FilePath-ascending order.
        /// </summary>
        public IEnumerable<LocalIndexRecord> GetRecords()
        {
            return _records.Values
                .OrderBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Try to get a record by canonical file path.
        /// </summary>
        public bool TryGet(string filePath, out LocalIndexRecord record)
        {
            var key = NormalizePath(filePath);
            return _records.TryGetValue(key, out record);
        }

        /// <summary>
        /// Get an existing record or create a new one with the specified FilePath and DocId.
        /// Caller is responsible for setting ContentHash, ActiveContentHash, etc.
        /// </summary>
        public LocalIndexRecord GetOrAdd(string filePath, string docId)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var key = NormalizePath(filePath);

            if (_records.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var rec = new LocalIndexRecord
            {
                FilePath = key,
                DocId = docId ?? string.Empty,
                LastIndexedUtc = DateTime.MinValue
            };

            _records[key] = rec;
            return rec;
        }

        /// <summary>
        /// Remove a record by file path, if present.
        /// </summary>
        public bool Remove(string filePath)
        {
            var key = NormalizePath(filePath);
            return _records.Remove(key);
        }

        /// <summary>
        /// Compute the set of records whose FilePath is not in the provided set of
        /// current files. This is useful for missing-file detection (IDX-0035).
        ///
        /// currentFiles should be canonical/relative paths using the same convention
        /// as FilePath (callers can normalize with NormalizePath).
        /// </summary>
        public IReadOnlyList<LocalIndexRecord> GetMissingFiles(IEnumerable<string> currentFiles)
        {
            if (currentFiles == null)
            {
                return _records.Values.ToList();
            }

            var set = new HashSet<string>(
                currentFiles.Select(NormalizePath),
                StringComparer.OrdinalIgnoreCase);

            var missing = _records.Values
                .Where(r => !set.Contains(r.FilePath))
                .OrderBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return missing;
        }

        /// <summary>
        /// Persist the index to disk using an atomic write strategy:
        ///   - Ensure folder exists
        ///   - Write JSON to a temp file
        ///   - Replace the final file with the temp file
        ///
        /// Guarantees from IDX-0036:
        ///   - Sorted by FilePath ascending
        ///   - Crash-safe because we save after each file (caller choice)
        /// </summary>
        public void Save()
        {
            var folder = Path.Combine(ProjectRoot, IndexFolder);
            Directory.CreateDirectory(folder);

            var finalPath = Path.Combine(folder, IndexFileName);
            var tempPath = finalPath + ".tmp-" + Guid.NewGuid().ToString("N");

            var list = GetRecords().ToList();

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null
            };

            var json = JsonSerializer.Serialize(list, options);
            File.WriteAllText(tempPath, json, Encoding.UTF8);

            // Best-effort atomic replace
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            File.Move(tempPath, finalPath);
        }

        /// <summary>
        /// Normalize paths for use as keys: use forward slashes and lower-case.
        /// This mirrors the canonical path style from IDX-003.
        /// </summary>
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

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
