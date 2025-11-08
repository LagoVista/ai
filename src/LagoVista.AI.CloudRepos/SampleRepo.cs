// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: eb116f4f70809844199ab6998e5c9f91143b7c168ee30db228debf734c13a356
// IndexVersion: 2
// --- END CODE INDEX META ---
using System.Threading.Tasks;
using LagoVista.AI.Models.TrainingData;
using LagoVista.CloudStorage.Storage;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.CloudRepos
{
    public class SampleRepo : TableStorageBase<Sample>, ISampleRepo
    {
        public SampleRepo(ITrainingDataSettings settings, IAdminLogger logger) : 
            base(settings.SampleConnectionSettings.AccountId, settings.SampleConnectionSettings.AccessKey, logger)
        {

        }

        public Task AddSampleAsync(Sample sample)
        {
            return this.InsertAsync(sample);
        }

        public Task DeleteSampleDetailsAsync(string sampleId, string orgId)
        {
            return this.RemoveAsync(orgId, sampleId);
        }

        public Task<Sample> GetSampleAsync(string sampleId, string orgId)
        {
            return this.GetAsync(orgId, sampleId);
        }
    }
}
