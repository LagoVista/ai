// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 295d16664fdf1cf1a49ff956cbd717e56738fe6e4b60103838328c075c78a620
// IndexVersion: 2
// --- END CODE INDEX META ---
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
        Task<TrainingDataSet> GetTrainingDataSetAsync(string id, EntityHeader org, EntityHeader user);
        Task<InvokeResult> DeleteTrainingDataSetManager(string id, EntityHeader org, EntityHeader user);
        Task<ListResponse<TrainingDataSetSummary>> GetForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest);
        Task<bool> QueryKeyInUse(string key, EntityHeader org);
        Task<DependentObjectCheckResult> CheckInUseAsync(string id, EntityHeader org, EntityHeader user);
    }
}
