using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.CloudRepos
{
    public class MLModelRepo : IMLModelRepo
    {
        IAdminLogger _adminLogger;
        IMLRepoSettings _settings;

        public MLModelRepo(IMLRepoSettings settings, IAdminLogger adminLogger)
        {
            _settings = settings;
            _adminLogger = adminLogger;
        }

        private string GetContainerName(string orgId)
        {
            return $"mlmodels{orgId}".ToLower();
        }

        private async Task<BlobContainerClient> CreateBlobContainerClient(String containerName)
        {
            var connectionString = $"DefaultEndpointsProtocol=https;AccountName={_settings.MLBlobStorage.AccountId};AccountKey={_settings.MLBlobStorage.AccessKey}";
            var blobClient = new BlobServiceClient(connectionString);
            try
            {
                var blobContainerClient = blobClient.GetBlobContainerClient(containerName);
                return blobContainerClient;
            }
            catch (Exception)
            {
                var container = await blobClient.CreateBlobContainerAsync(containerName);
                return container.Value;
            }
        }

        private string GetBlobName(string modelId, int revisionId)
        {
            return $"{modelId}.{revisionId}.model";
        }

        public async Task<InvokeResult> AddModelAsync(string orgId, string modelId, int revisionId, byte[] model)
        {
            var retryCount = 0;
            Exception ex = null;
            while (retryCount++ < 5)
            {
                try
                {
                    var containerClient = await CreateBlobContainerClient(GetContainerName(orgId));
                    var blob = containerClient.GetBlobClient(GetBlobName(modelId, revisionId));
                    await blob.UploadAsync(new BinaryData(model));
                    return InvokeResult.Success;
                }
                catch (Exception exc)
                {
                    ex = exc;
                    _adminLogger.AddException("MLModelRepo_GetModelAsync", ex);
                    Console.WriteLine("Exception deserializeing: " + ex.Message);
                    await Task.Delay(retryCount * 250);
                }
            }

            return InvokeResult.FromException("MLModelRepo_GetModelAsync", ex);
        }

        public async Task<InvokeResult<byte[]>> GetModelAsync(string orgId, string modelId, int revisionId)
        {
            var retryCount = 0;
            Exception ex = null;
            while (retryCount++ < 5)
            {
                try
                {
                    var containerClient = await CreateBlobContainerClient(GetContainerName(orgId));
                    var blob = containerClient.GetBlobClient(GetBlobName(modelId, revisionId));

                    var response = await blob.DownloadContentAsync();
                    var buffer = response.Value;

                    return InvokeResult<byte[]>.Create(buffer.Content.ToArray());
                }
                catch (Exception exc)
                {
                    ex = exc;
                    _adminLogger.AddException("MLModelRepo_GetModelAsync", ex);
                    Console.WriteLine("Exception deserializeing: " + ex.Message);
                    await Task.Delay(retryCount * 250);
                }
            }

            return InvokeResult<byte[]>.FromException("MLModelRepo_GetModelAsync", ex);
        }
    }
}
