using LagoVista.AI.Models.TrainingData;
using LagoVista.CloudStorage.Storage;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    class SampleLabelRepo : TableStorageBase<SampleLabel>, ISampleLabelRepo
    {
        public SampleLabelRepo(ITrainingDataSettings settings, IAdminLogger logger) :
            base(settings.SampleConnectionSettings.AccountId, settings.SampleConnectionSettings.AccessKey, logger)
        {

        }

        public Task AddSampleLabelAsync(SampleLabel label)
        {
            return this.InsertAsync(label);
        }

        public Task<ListResponse<SampleLabel>> GetSamplesForLabelAsync(string labelId, ListRequest request)
        {
            return GetPagedResultsAsync(labelId, request);
        }

        public Task RemoveSampleLabelAsync(string labelId, string sampleId)
        {
            return this.RemoveAsync(labelId, sampleId);
        }
    }
}
