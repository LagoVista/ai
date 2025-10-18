using System.Text.Json;
using System.Text.Json.Serialization;

namespace RagCli.Services
{
    /// <summary>
    /// Maintains a small registry file at the repository root that maps
    /// relative file paths -> Qdrant point IDs (for deletion/cleanup).
    ///
    /// Default filename: ".code-index.json"
    ///
    /// This pairs with FileManifestTrackerInline, which stores only the
    /// content hash in each C# file's comment block. The registry does
    /// NOT store timestamps, only point IDs per file for cleanup.
    /// </summary>
    public sealed class IndexRegistry
    {
        private readonly string _repoRoot;
        private readonly string _registryPath;
        private IndexRegistryData _data;
        private readonly JsonSerializerOptions _jsonOpts = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public IndexRegistry(string repoRoot, string fileName = ".code-index.json")
        {
            _repoRoot = Path.GetFullPath(repoRoot);
            _registryPath = Path.Combine(_repoRoot, fileName);
            _data = LoadOrCreate(_registryPath);
        }

        /// <summary>
        /// Convert to normalized repo-relative key (forward slashes).
        /// </summary>
        public string ToKey(string fullPath)
        {
            var rel = Path.GetRelativePath(_repoRoot, fullPath);
            return rel.Replace('\\', '/');
        }

        /// <summary>
        /// Add or replace the set of point IDs for a file.
        /// </summary>
        public void Upsert(string repoRelativePath, IEnumerable<string> pointIds)
        {
            repoRelativePath = Normalize(repoRelativePath);
            if (!_data.Files.TryGetValue(repoRelativePath, out var entry))
            {
                entry = new IndexRegistryEntry();
                _data.Files[repoRelativePath] = entry;
            }

            entry.PointIds = pointIds?.Distinct().OrderBy(x => x).ToList() ?? new List<string>();
        }

        /// <summary>
        /// Remove entries for files that are no longer present on disk.
        /// Returns a list of point IDs that should be deleted from Qdrant.
        /// </summary>
        public List<string> RemoveMissing(IEnumerable<string> currentRepoRelativePaths)
        {
            var current = new HashSet<string>(currentRepoRelativePaths.Select(Normalize));
            var removedPointIds = new List<string>();

            var toDelete = _data.Files.Keys.Where(k => !current.Contains(k)).ToList();
            foreach (var key in toDelete)
            {
                if (_data.Files.TryGetValue(key, out var entry) && entry.PointIds is not null)
                {
                    removedPointIds.AddRange(entry.PointIds);
                }
                _data.Files.Remove(key);
            }

            return removedPointIds;
        }

        /// <summary>
        /// Get all known repo-relative paths in the registry.
        /// </summary>
        public IReadOnlyCollection<string> GetKnownPaths() => _data.Files.Keys.ToList();

        /// <summary>
        /// Get all point IDs for a given repo-relative path, or empty if none.
        /// </summary>
        public IReadOnlyList<string> GetPointIds(string repoRelativePath)
        {
            repoRelativePath = Normalize(repoRelativePath);
            return _data.Files.TryGetValue(repoRelativePath, out var e) && e.PointIds is not null
                ? e.PointIds
                : Array.Empty<string>();
        }

        /// <summary>
        /// Persist registry to disk.
        /// </summary>
        public void Save()
        {
            var dir = Path.GetDirectoryName(_registryPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir!);
            _data.UpdatedAtUtc = DateTime.UtcNow;
            File.WriteAllText(_registryPath, JsonSerializer.Serialize(_data, _jsonOpts));
        }

        /// <summary>
        /// Registry filename on disk.
        /// </summary>
        public string RegistryPath => _registryPath;

        private static IndexRegistryData LoadOrCreate(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<IndexRegistryData>(json);
                    if (data is not null) return data;
                }
            }
            catch
            {
                // fallthrough to create new
            }

            return new IndexRegistryData
            {
                Version = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Files = new Dictionary<string, IndexRegistryEntry>(StringComparer.OrdinalIgnoreCase)
            };
        }

        private static string Normalize(string repoRelativePath)
            => repoRelativePath.Replace('\\', '/');
    }

    public sealed class IndexRegistryData
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("createdAtUtc")]
        public DateTime CreatedAtUtc { get; set; }

        [JsonPropertyName("updatedAtUtc")]
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>
        /// Map of repo-relative path -> point IDs for the file's current chunks.
        /// Example key: "src/Services/AuthService.cs"
        /// </summary>
        [JsonPropertyName("files")]
        public Dictionary<string, IndexRegistryEntry> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class IndexRegistryEntry
    {
        [JsonPropertyName("pointIds")]
        public List<string> PointIds { get; set; } = new();
    }
}
