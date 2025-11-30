using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SharpCompress.Common;

namespace LagoVista.AI.Rag.ContractPacks.IndexStore.Services
{
    /// <summary>
    /// JSON-backed implementation of ILocalIndexStore using the canonical
    /// LocalIndexStore and LocalIndexRecord models and Newtonsoft.Json.
    /// </summary>
    public sealed class JsonLocalIndexStore : ILocalIndexStore
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        public JsonLocalIndexStore()
        {

        }

     
        public async Task<LocalIndexStore> LoadAsync(IngestionConfig config, string repoId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(repoId))
                throw new ArgumentNullException(nameof(repoId));

            var path = GetIndexPath(config, repoId);

            if (!File.Exists(path))
            {
                return new LocalIndexStore
                {
                    RepoId = repoId,
                    Records = new Dictionary<string, LocalIndexRecord>(StringComparer.OrdinalIgnoreCase)
                };
            }

            var json = await File.ReadAllTextAsync(path, token).ConfigureAwait(false);
            var store = JsonConvert.DeserializeObject<LocalIndexStore>(json, Settings) ?? new LocalIndexStore();
            if (string.IsNullOrWhiteSpace(store.RepoId))
                store.RepoId = repoId;

            if (store.Records == null)
                store.Records = new Dictionary<string, LocalIndexRecord>(StringComparer.OrdinalIgnoreCase);

            return store;
        }

        public async Task SaveAsync(IngestionConfig config, string repoId, LocalIndexStore store, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(repoId))
                throw new ArgumentNullException(nameof(repoId));
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            var path = GetIndexPath(config, repoId);

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            store.RepoId = repoId;
            var json = JsonConvert.SerializeObject(store, Settings);
            await File.WriteAllTextAsync(path, json, token).ConfigureAwait(false);
        }

        public IReadOnlyList<LocalIndexRecord> GetAll(LocalIndexStore store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            var list = new List<LocalIndexRecord>();
            foreach (var record in store.GetAll())
            {
                list.Add(record);
            }

            return list;
        }

        private string GetIndexPath(IngestionConfig config, string repoId)
        {
            var safeRepoId = repoId.Replace('\\', '_').Replace('/', '_');
            return Path.Combine(config.Ingestion.SourceRoot, repoId, safeRepoId + ".local-index.json");
        }
    }
}
