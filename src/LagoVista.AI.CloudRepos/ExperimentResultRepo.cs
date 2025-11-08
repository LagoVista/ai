// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 8c065e6959bb00d0eb3f98f56e0251c40695497ac9dd0249e8529476be7547c9
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.CloudRepos.Models;
using LagoVista.AI.Models;
using LagoVista.CloudStorage.Storage;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    public class ExperimentResultRepo : TableStorageBase<ExperimentResultDTO>, IExperimentResultRepo
    {
        public ExperimentResultRepo(IMLRepoSettings settings, IAdminLogger logger) : base(settings.MLTableStorage.AccountId, settings.MLTableStorage.AccessKey, logger)
        {
        }

        public Task AddResultAsync(ExperimentResult result)
        {
            return this.InsertAsync(ExperimentResultDTO.Create(result));
        }

        public async Task<ListResponse<ExperimentResult>> GetResultsAsync(string modelId, int versionNumber, ListRequest listRequest)
        {
            var results = await GetPagedResultsAsync(Models.ExperimentResultDTO.GetPartitionKey(modelId, versionNumber), listRequest);
            return ListResponse<ExperimentResult>.Create(results.Model.Select(res => res.ToExpermentResult()));
        }
    }
}
