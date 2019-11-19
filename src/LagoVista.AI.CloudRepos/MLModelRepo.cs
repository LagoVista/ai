using System;
using System.IO;
using System.Threading.Tasks;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.WindowsAzure.Storage.Blob;

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

        private CloudBlobClient CreateBlobClient()
        {
            var baseuri = $"https://{_settings.MLBlobStorage.AccountId}.blob.core.windows.net";

            var uri = new Uri(baseuri);
            return new CloudBlobClient(uri, new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials(_settings.MLBlobStorage.AccountId, _settings.MLBlobStorage.AccessKey));
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
                    var cloudClient = CreateBlobClient();
                    var primaryContainer = cloudClient.GetContainerReference(GetContainerName(orgId));
                    await primaryContainer.CreateIfNotExistsAsync();
                    var blob = primaryContainer.GetBlockBlobReference(GetBlobName(modelId, revisionId));
                    await blob.UploadFromByteArrayAsync(model, 0, model.Length);
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
                    var cloudClient = CreateBlobClient();
                    var primaryContainer = cloudClient.GetContainerReference(GetContainerName(orgId));
                    await primaryContainer.CreateIfNotExistsAsync();

                    var blobName = GetBlobName(modelId, revisionId);
                    var blob = primaryContainer.GetBlobReference(blobName);

                    using (var ms = new MemoryStream())
                    {
                        await blob.DownloadToStreamAsync(ms);
                        ms.Seek(0, SeekOrigin.Begin);
                        return InvokeResult<byte[]>.Create(ms.ToArray());
                    }
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
