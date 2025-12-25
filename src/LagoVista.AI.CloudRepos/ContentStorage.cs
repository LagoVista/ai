using LagoVista.AI.Rag.ContractPacks.Content.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.CloudStorage.Storage;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Diagnostics;
using System.Threading.Tasks;


namespace LagoVista.AI.CloudRepos
{
    public class ContentStorage : CloudFileStorage, IContentStorage
    {
        private readonly string _containerName;
        private readonly IAdminLogger _adminLogger;

        public ContentStorage(IngestionConfig config, IAdminLogger adminLogger) : base(config.ContentRepo.AccountId, config.ContentRepo.AccessKey, adminLogger)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _containerName = GetContainerName(config.OrgId);
        }

        private string GetContainerName(string orgId)
        {
            return $"llmcontent{orgId}".ToLower();
        }

        public async Task<InvokeResult> AddContntAsync(string blobName, string content)
        {
            var sw = Stopwatch.StartNew();
            var result = await AddFileAsync(_containerName, blobName, content);
            _adminLogger.Trace($"[ContentStorage__AddContentAsync] - Added {blobName}, Size: {content.Length} bytes in {sw.Elapsed.TotalMilliseconds}ms");
            return result.ToInvokeResult();
        }

        public Task<InvokeResult> AddContentAsync(string blobName, byte[] content)
        {
            return AddContntAsync(blobName, System.Text.UTF8Encoding.UTF8.GetString(content));
        }
    }
}
