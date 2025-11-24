using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace LagoVista.AI.Rag.ContractPacks.IndexStore.Services
{
    /// <summary>
    /// JSON-backed implementation of ILocalIndexStore using the canonical
    /// LocalIndexStore and LocalIndexRecord models and Newtonsoft.Json.
    /// </summary>
    public sealed class JsonLocalIndexStore : ILocalIndexStore
    {
        private readonly string _rootFolder;
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        public JsonLocalIndexStore()
        {

        }

        public JsonLocalIndexStore(string rootFolder)
        {
            if (string.IsNullOrWhiteSpace(rootFolder))
                throw new ArgumentNullException(nameof(rootFolder));

            _rootFolder = rootFolder;
        }

        public async Task<LocalIndexStore> LoadAsync(string repoId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(repoId))
                throw new ArgumentNullException(nameof(repoId));

            Directory.CreateDirectory(_rootFolder);
            var path = GetIndexPath(repoId);

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

        public async Task SaveAsync(string repoId, LocalIndexStore store, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(repoId))
                throw new ArgumentNullException(nameof(repoId));
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            Directory.CreateDirectory(_rootFolder);
            var path = GetIndexPath(repoId);

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

        private string GetIndexPath(string repoId)
        {
            var safeRepoId = repoId.Replace('\\', '_').Replace('/', '_');
            return Path.Combine(_rootFolder, safeRepoId + ".local-index.json");
        }
    }
}
