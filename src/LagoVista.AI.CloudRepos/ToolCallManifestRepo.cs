using Azure;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models.Context;
using LagoVista.CloudStorage.Storage;
using LagoVista.Core;
using LagoVista.Core.Exceptions;
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
        private readonly IAdminLogger _adminlogger;

        public ToolCallManifestRepo(IMLRepoSettings settings, IAdminLogger adminLogger, ICacheProvider cacheProvider) : base(settings.MLBlobStorage.AccountId, settings.MLBlobStorage.AccessKey, adminLogger)
        {
            _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
            _adminlogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        private string GetContainerName(string orgId)
        {
            return $"clienttools{orgId}".ToLower();
        }

        private string BuildPath(string orgId, string toolManifestId)
        {
            return $"{orgId}/clienttool/manifests/{toolManifestId}.json".ToLower();
        }

        public async Task<ToolCallManifest> GetToolCallManifestAsync(string orgId, string toolManifestId)
        {
            _adminlogger.Trace($"{this.Tag()} - get toolcast manifest", orgId.ToKVP("orgId"), toolManifestId.ToKVP("toolManifestId"));

            var path = BuildPath(orgId, toolManifestId);
            var json = await _cacheProvider.GetAsync($"{orgId}.{toolManifestId}");
            if (!string.IsNullOrEmpty(json))
            {
                return JsonConvert.DeserializeObject<ToolCallManifest>(json);
            }

            var containerName = GetContainerName(orgId);
            var buffer = await GetFileAsync(containerName, path);
            if (buffer.Successful)
            {
                return JsonConvert.DeserializeObject<ToolCallManifest>(System.Text.UTF8Encoding.UTF8.GetString(buffer.Result));
            }
            else
                throw new RecordNotFoundException(nameof(ToolCallManifest), toolManifestId);
        }

        public async Task RemoveToolCallManifestAsync(string orgId, string toolManifestId)
        {
            _adminlogger.Trace($"{this.Tag()} - remove toolcast manifest", orgId.ToKVP("orgId"), toolManifestId.ToKVP("toolManifestId"));

            await _cacheProvider.RemoveAsync($"{orgId}.{toolManifestId}");
            await DeleteFileAsync(GetContainerName(orgId), BuildPath(orgId, toolManifestId)); 
        }

        public async Task SetCallToolManifestAsync(string orgId, string toolManifestId, ToolCallManifest toolManifest)
        {
            _adminlogger.Trace($"{this.Tag()} - add toolcast manifest", orgId.ToKVP("orgId"), toolManifestId.ToKVP("toolManifestId"));

            var containerName = GetContainerName(orgId);
            var json = JsonConvert.SerializeObject(toolManifest);
            var path = BuildPath(orgId, toolManifestId);
            var buffer = System.Text.UTF8Encoding.UTF8.GetBytes(json);
            var result = await AddFileAsync(containerName, path, buffer);
            if (!result.Successful)
                throw new RequestFailedException($"could not add tool call manifst {orgId} - {toolManifest}");

            await _cacheProvider.AddAsync($"{orgId}.{toolManifestId}", json);
        }
    }
}
