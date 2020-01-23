using LagoVista.Core.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

        private CloudBlobClient CreateBlobClient()
        {
            var baseuri = $"https://{_settings.AccountId}.blob.core.windows.net";

            var uri = new Uri(baseuri);
            return new CloudBlobClient(uri, new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials(_settings.AccountId, _settings.AccessKey));
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
                    var cloudClient = CreateBlobClient();
                    var primaryContainer = cloudClient.GetContainerReference(GetContainerName(orgId));
                    await primaryContainer.CreateIfNotExistsAsync();
                    var blob = primaryContainer.GetBlockBlobReference(sampleId);
                    await blob.UploadFromByteArrayAsync(sample, 0, sample.Length);
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
                    var cloudClient = CreateBlobClient();
                    var primaryContainer = cloudClient.GetContainerReference(GetContainerName(orgId));
                    await primaryContainer.CreateIfNotExistsAsync();

                    var blob = primaryContainer.GetBlobReference(sampleId);

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
                    var cloudClient = CreateBlobClient();
                    var primaryContainer = cloudClient.GetContainerReference(GetContainerName(orgId));
                    await primaryContainer.CreateIfNotExistsAsync();
                    var blob = primaryContainer.GetBlockBlobReference(sampleId);
                    await blob.UploadFromByteArrayAsync(sample, 0, sample.Length);
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
