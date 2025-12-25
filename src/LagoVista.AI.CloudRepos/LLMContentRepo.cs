// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 9e6b7fb3b94cf3f8a81e030e3b9f68cb88a6af3a13b48ea206f7cd75ba16e1dc
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.CloudStorage.Storage;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using MongoDB.Bson.IO;
using System;
using System.Globalization;
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
            return InvokeResult<string>.Create(System.Text.UTF8Encoding.UTF8.GetString(result.Result));
        }

        private const int BlobMaxLength = 1024;

        private const int BlobFileDirectoryMinLength = 1;

        public void ValidateBlobName(string blobName)
        {
            if (string.IsNullOrWhiteSpace(blobName))
            {
                throw new ArgumentException("Blob name must not be empty");
            }

            if (blobName.Length < BlobFileDirectoryMinLength || blobName.Length > BlobMaxLength)
            {
                throw new ArgumentException($"Blob name must be between {BlobFileDirectoryMinLength} and {BlobMaxLength} characters");
            }

            int slashCount = 0;
            foreach (char c in blobName)
            {
                if (c == '/')
                {
                    slashCount++;
                }
            }

            // 254 slashes means 255 path segments; max 254 segments for blobs, 255 includes container. 
            if (slashCount >= 254)
            {
                throw new ArgumentException("Too many paths in blob name");
            }

        }      

        public async Task<InvokeResult> AddTextContentAsync(string orgId, string accountId, string apiKey, string blobName, string content)
        {
            InitConnectionSettings(accountId, apiKey);
            var containerName = GetContainerName(orgId);
            var result = await AddFileAsync(containerName, blobName, content);
            return result.ToInvokeResult();
        }

        public async Task<InvokeResult<string>> GetTextContentAsync(AgentContext vectorDb, string blobName)
        {
            InitConnectionSettings(vectorDb.AzureAccountId, vectorDb.AzureApiToken);
            var result = await GetFileAsync(vectorDb.BlobContainerName, blobName);
            if(!result.Successful)
            {
                return InvokeResult<string>.FromInvokeResult(result.ToInvokeResult());
            }

            var content = System.Text.UTF8Encoding.UTF8.GetString(result.Result);
            return InvokeResult<string>.Create(content);
        }
    }
}
