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
            return $"{path.Replace('\\','/')}/{fileName}";
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
            var result = await GetFileAsync(vectorDb.BlobContainerName, blobName);
            return InvokeResult<string>.Create(System.Text.ASCIIEncoding.ASCII.GetString(result.Result));
        }
    }
}
