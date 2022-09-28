using Azure.Storage.Blobs;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    class SampleMediaRepo : ISampleMediaRepo
    {
        IConnectionSettings _settings;
        IAdminLogger _adminLogger;
        public SampleMediaRepo(ITrainingDataSettings connectionSettings, IAdminLogger adminLogger)
        {
            _settings = connectionSettings.SampleMediaConnectionsSettings;
            _adminLogger = adminLogger;
        }

        private async Task<BlobContainerClient> CreateBlobContainerClient(String containerName)
        {
            var connectionString = $"DefaultEndpointsProtocol=https;AccountName={_settings.AccountId};AccountKey={_settings.AccessKey}";
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

        private string GetContainerName(string orgId)
        {
            return $"mlsamples{orgId}".ToLower();
        }

        public async Task<InvokeResult> AddSampleAsync(string orgId, string sampleId, byte[] sample)
        {
            var retryCount = 0;
            Exception ex = null;
            while (retryCount++ < 5)
            {
                try
                {
                    var cloudClient = await CreateBlobContainerClient(GetContainerName(orgId));
                    var blobClient = cloudClient.GetBlobClient(sampleId);
                    
                    await blobClient.UploadAsync(new BinaryData(sample));
                    return InvokeResult.Success;
                }
                catch (Exception exc)
                {
                    ex = exc;
                    _adminLogger.AddException("SampleMediaRepo_AddSampleAsync", ex);
                    Console.WriteLine("Exception deserializeing: " + ex.Message);
                    await Task.Delay(retryCount * 250);
                }
            }

            return InvokeResult.FromException("SampleMediaRepo_AddSampleAsync", ex);

        }

        public async Task<InvokeResult<byte[]>> GetSampleAsync(string orgId, string sampleId)
        {
            var retryCount = 0;
            Exception ex = null;
            while (retryCount++ < 5)
            {
                try
                {
                    var cloudClient = await CreateBlobContainerClient(GetContainerName(orgId));
                    var blobClient = cloudClient.GetBlobClient(sampleId);

                    var result = await blobClient.DownloadContentAsync();

                    return InvokeResult<byte[]>.Create(result.Value.Content.ToArray());
                }
                catch (Exception exc)
                {
                    ex = exc;
                    _adminLogger.AddException("SampleMediaRepo_GetSampleAsync", ex);
                    Console.WriteLine("Exception deserializeing: " + ex.Message);
                    await Task.Delay(retryCount * 250);
                }
            }

            return InvokeResult<byte[]>.FromException("SampleMediaRepo_GetSampleAsync", ex);
        }

        public async Task<InvokeResult> UpdateSampleAsync(string orgId, string sampleId, byte[] sample)
        {
            var retryCount = 0;
            Exception ex = null;
            while (retryCount++ < 5)
            {
                try
                {
                    var cloudClient = await CreateBlobContainerClient(GetContainerName(orgId));
                    var blobClient = cloudClient.GetBlobClient(sampleId);
                    var result = await blobClient.DownloadContentAsync();
                    return InvokeResult.Success;
                }
                catch (Exception exc)
                {
                    ex = exc;
                    _adminLogger.AddException("SampleMediaRepo_AddSampleAsync", ex);
                    Console.WriteLine("Exception deserializeing: " + ex.Message);
                    await Task.Delay(retryCount * 250);
                }
            }

            return InvokeResult.FromException("SampleMediaRepo_AddSampleAsync", ex);
        }
    }
}
