using LagoVista.AI.Models.TrainingData;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface ITrainingDataSetRepo
    {
        Task AddTrainingDataSetsAsync(TrainingDataSet label);
        Task UpdateTrainingDataSetsAsync(TrainingDataSet label);
        Task<TrainingDataSet> GetTrainingDataSetAsync(string id);
        Task<ListResponse<TrainingDataSetSummary>> GetTrainingDataSetsForOrgAsync(string orgId, ListRequest request);
        Task<bool> QueryKeyInUseAsync(string key, string org);
    }
}
