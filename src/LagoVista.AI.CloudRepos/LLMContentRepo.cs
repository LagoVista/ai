using LagoVista.AI.Models;
using LagoVista.CloudStorage.Storage;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
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

        public async Task<InvokeResult> AddImageContentAsync(string orgId, string path, string fileName, byte[] model, string contentType)
        {
            var blobName = GetBlobName(path, fileName);
            var containerName = GetContainerName(orgId);
            var result = await AddFileAsync(containerName, blobName, model);
            return result.ToInvokeResult();
        }

        public async Task<InvokeResult<byte[]>> GetImageContentAsync(string orgId, string path, string fileName)
        {
            var blobName = GetBlobName(path, fileName);
            var containerName = GetContainerName(orgId);
            return await GetFileAsync(containerName, blobName);
        }


        public async Task<InvokeResult> AddTextContentAsync(string orgId, string path, string fileName, string content, string contentType)
        {
            var blobName = GetBlobName(path, fileName);
            var containerName = GetContainerName(orgId);
            var result = await AddFileAsync(containerName, blobName, content);
            return result.ToInvokeResult();
        }

        public async Task<InvokeResult<string>> GetTextContentAsync(string orgId, string path, string fileName)
        {
            var blobName = GetBlobName(path, fileName);
            var containerName = GetContainerName(orgId);
            var result = await GetFileAsync(containerName, blobName);
            return InvokeResult<string>.Create(System.Text.ASCIIEncoding.ASCII.GetString(result.Result));
        }
    }
}
