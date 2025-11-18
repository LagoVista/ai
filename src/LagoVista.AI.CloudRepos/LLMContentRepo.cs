// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 9e6b7fb3b94cf3f8a81e030e3b9f68cb88a6af3a13b48ea206f7cd75ba16e1dc
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.CloudStorage.Storage;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    public class LLMContentRepo : CloudFileStorage, ILLMContentRepo
    {
        public LLMContentRepo(IMLRepoSettings settings, IAdminLogger adminLogger) : base(settings.MLBlobStorage.AccountId, settings.MLBlobStorage.AccessKey, adminLogger)
        {
        }

        private string GetContainerName(string orgId)
        {
            return $"llmcontent{orgId}".ToLower();
        }

        private string GetBlobName(string path, string fileName)
        {
            return $"{path.Replace('\\','/')}/{fileName}".ToLower();
        }

        public async Task<InvokeResult> AddImageContentAsync(AgentContext vectorDb, string path, string fileName, byte[] model, string contentType)
        {
            var blobName = GetBlobName(path, fileName);
            var containerName = GetContainerName(vectorDb.OwnerOrganization.Id);
            var result = await AddFileAsync(containerName, blobName, model);
            return result.ToInvokeResult();
        }

        public async Task<InvokeResult<byte[]>> GetImageContentAsync(AgentContext vectorDb, string path, string fileName)
        {
            var blobName = GetBlobName(path, fileName);
            var containerName = GetContainerName(vectorDb.OwnerOrganization.Id);
            return await GetFileAsync(containerName, blobName);
        }


        public async Task<InvokeResult> AddTextContentAsync(AgentContext vectorDb, string path, string fileName, string content, string contentType)
        {
            var blobName = GetBlobName(path, fileName);
            var containerName = GetContainerName(vectorDb.OwnerOrganization.Id);
            var result = await AddFileAsync(containerName, blobName, content);
            return result.ToInvokeResult();
        }

        public async Task<InvokeResult<string>> GetTextContentAsync(AgentContext vectorDb, string path, string fileName)
        {
            InitConnectionSettings(vectorDb.AzureAccountId, vectorDb.AzureApiToken);
            
            var blobName = GetBlobName(path, fileName);
            var containerName = GetContainerName(vectorDb.OwnerOrganization.Id);
            var result = await GetFileAsync(containerName, blobName);
            return InvokeResult<string>.Create(System.Text.ASCIIEncoding.ASCII.GetString(result.Result));
        }
    }
}
