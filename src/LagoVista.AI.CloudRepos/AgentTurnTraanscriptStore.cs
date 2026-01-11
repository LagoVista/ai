using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models;
using LagoVista.CloudStorage.Storage;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    public class AgentTurnTraanscriptStore : CloudFileStorage, IAgentTurnTranscriptStore
    {
        public AgentTurnTraanscriptStore(IMLRepoSettings settings, IAdminLogger adminLogger) : base(settings.MLBlobStorage.AccountId, settings.MLBlobStorage.AccessKey, adminLogger)
        {

        }

        private string GetContainerName(string orgId)
        {
            return $"agentsessions{orgId}".ToLower();
        }


        private string BuildPath(string type, string orgId, string sessionId, string turnId)
        {
            return $"{orgId}/sessions/{sessionId}/turns/{turnId}/{type}.json".ToLower();
        }       

        public async Task<InvokeResult<string>> LoadTurnRequestAsync(string orgId, string sessionId, string turnId, CancellationToken cancellationToken = default)
        {
            var path = BuildPath("request", orgId, sessionId, turnId);
            var containerName = GetContainerName(orgId);
            var buffer = await GetFileAsync(containerName, path);
            return InvokeResult<string>.Create(System.Text.UTF8Encoding.UTF8.GetString(buffer.Result));
        }

        public async Task<InvokeResult<string>> LoadTurnResponseAsync(string orgId, string sessionId, string turnId, CancellationToken cancellationToken = default)
        {
            var path = BuildPath("response", orgId, sessionId, turnId);
            var containerName = GetContainerName(orgId);
            var buffer = await GetFileAsync(containerName, path);
            return InvokeResult<string>.Create(System.Text.UTF8Encoding.UTF8.GetString(buffer.Result));
        }

        public async Task<InvokeResult<System.Uri>> SaveTurnRequestAsync(string orgId, string sessionId, string turnId, string requestJson, CancellationToken cancellationToken = default)
        {
            var path = BuildPath("request", orgId, sessionId, turnId);
            var containerName = GetContainerName(orgId);
            return await AddFileAsync(containerName, path, requestJson);
        }

        public async Task<InvokeResult<System.Uri>> SaveTurnResponseAsync(string orgId, string sessionId, string turnId, string responseJson, CancellationToken cancellationToken = default)
        {
            var path = BuildPath("response", orgId, sessionId, turnId);
            var containerName = GetContainerName(orgId);
            return await AddFileAsync(containerName, path, responseJson);
        }
    }
}
