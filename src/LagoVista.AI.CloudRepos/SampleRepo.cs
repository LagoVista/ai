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
