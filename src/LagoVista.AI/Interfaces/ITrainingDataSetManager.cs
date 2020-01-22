using LagoVista.AI.Models.TrainingData;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface ITrainingDataSetManager
    {
        Task<InvokeResult> AddTrainingDataSetManager(TrainingDataSet dataSet, EntityHeader org, EntityHeader user);
        Task<InvokeResult> UpdateTrainingDataSetManager(TrainingDataSet dataSet, EntityHeader org, EntityHeader user);
        Task<InvokeResult<TrainingDataSet>> GetTrainingDataSetAsync(string id, EntityHeader org, EntityHeader user);
        Task<InvokeResult> DeleteTrainingDataSetManager(string id, EntityHeader org, EntityHeader user);
        Task<ListResponse<TrainingDataSetSummary>> GetForOrgAsync(string id, EntityHeader org, EntityHeader user, ListRequest listRequest);
        Task<bool> QueryKeyInUse(string key, EntityHeader org);
        Task<DependentObjectCheckResult> CheckInUseAsync(string id, EntityHeader org, EntityHeader user);
    }
}
