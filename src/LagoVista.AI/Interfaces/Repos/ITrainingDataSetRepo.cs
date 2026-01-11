// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 8e0c14956bd41850b097799f5bd32a7d3f98192e87aa94668d4d5ee33534a91d
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.TrainingData;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Repos
{
    public interface ITrainingDataSetRepo
    {
        Task AddTrainingDataSetsAsync(TrainingDataSet dataSet);
        Task UpdateTrainingDataSetsAsync(TrainingDataSet dataSet);
        Task<TrainingDataSet> GetTrainingDataSetAsync(string id);
        Task<ListResponse<TrainingDataSetSummary>> GetTrainingDataSetsForOrgAsync(string orgId, ListRequest request);
        Task<bool> QueryKeyInUseAsync(string key, string org);
    }
}
