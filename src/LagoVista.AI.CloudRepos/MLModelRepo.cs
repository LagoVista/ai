using System.Threading.Tasks;
using LagoVista.CloudStorage.Storage;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.CloudRepos
{
    public class MLModelRepo : CloudFileStorage, IMLModelRepo
    {
        public MLModelRepo(IMLRepoSettings settings, IAdminLogger adminLogger) : base(settings.MLBlobStorage.AccountId, settings.MLBlobStorage.AccessKey, adminLogger)
        {
        }

        private string GetContainerName(string orgId)
        {
            return $"mlmodels{orgId}".ToLower();
        }

        private string GetBlobName(string modelId, int revisionId)
        {
            return $"{modelId}.{revisionId}.model";
        }

        public async Task<InvokeResult> AddModelAsync(string orgId, string modelId, int revisionId, byte[] model)
        {
            var blobName = GetBlobName(modelId, revisionId);
            var containerName = GetContainerName(orgId);
            var result = await AddFileAsync(containerName, blobName, model);
            return result.ToInvokeResult();
        }

        public async Task<InvokeResult<byte[]>> GetModelAsync(string orgId, string modelId, int revisionId)
        {
            var blobName = GetBlobName(modelId, revisionId);
            var containerName = GetContainerName(orgId);
            return await GetFileAsync(containerName, blobName);
        }
    }
}
