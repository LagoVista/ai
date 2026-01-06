using LagoVista.AI.Interfaces;
using LagoVista.AI.Models.Context;
using LagoVista.CloudStorage.Storage;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    public class ToolCallManifestRepo : CloudFileStorage, IToolCallManifestRepo
    {
        private readonly ICacheProvider _cacheProvider;

        public ToolCallManifestRepo(IMLRepoSettings settings, IAdminLogger adminLogger, ICacheProvider cacheProvider) : base(settings.MLBlobStorage.AccountId, settings.MLBlobStorage.AccessKey, adminLogger)
        {
            _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
        }

        private string GetContainerName(string orgId)
        {
            return $"clienttools{orgId}".ToLower();
        }

        private string BuildPath(string orgId, string toolManifestId)
        {
            return $"{orgId}/clienttool/manifests/{toolManifestId}.json".ToLower();
        }

        public async Task<ToolCallManifest> GetToolCallManifestAsync(string toolManifestId, string orgId)
        {
            var path = BuildPath(orgId, toolManifestId);
            var json = await _cacheProvider.GetAsync(toolManifestId);
            if (!string.IsNullOrEmpty(json))
            {
                return JsonConvert.DeserializeObject<ToolCallManifest>(json);
            }

            var containerName = GetContainerName(orgId);
            var buffer = await GetFileAsync(containerName, path);
            var manifest = JsonConvert.DeserializeObject<ToolCallManifest>(System.Text.UTF8Encoding.UTF8.GetString(buffer.Result));
            return manifest;
        }

        public async Task RemoveToolCallManifestAsync(string toolManifestId, string orgId)
        {
            await _cacheProvider.RemoveAsync($"{orgId}.{toolManifestId}");
            await DeleteFileAsync(BuildPath(orgId, toolManifestId), GetContainerName(orgId)); 
        }

        public async Task SetCallToolManifestAsync(string orgId, string toolManifestId, ToolCallManifest toolManifest)
        {
            var containerName = GetContainerName(orgId);
            var json = JsonConvert.SerializeObject(toolManifest);
            var path = BuildPath(orgId, toolManifestId);
            var buffer = System.Text.UTF8Encoding.UTF8.GetBytes(json);
            await AddFileAsync(containerName, path, buffer);
        }
    }
}
