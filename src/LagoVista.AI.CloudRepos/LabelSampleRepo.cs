// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: bed89c3d6bc84dd4fec791b62eaed02d943948ca19820d36bc452db7d5682402
// IndexVersion: 2
// --- END CODE INDEX META ---
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models.TrainingData;
using LagoVista.CloudStorage.Storage;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.CloudRepos
{
    public class LabelSampleRepo : TableStorageBase<LabelSample>, ILabelSampleRepo
    {
        public LabelSampleRepo(ITrainingDataSettings settings, IAdminLogger logger) :
            base(settings.SampleConnectionSettings.AccountId, settings.SampleConnectionSettings.AccessKey, logger)
        {

        }

        public Task AddLabelSampleAsync(LabelSample label)
        {
            return InsertAsync(label);
        }

        public async Task<List<LabelSample>> GetLabelsForSampleAsync(string sampleId)
        {
            return new List<LabelSample>(await base.GetByParitionIdAsync(sampleId));
        }

        public Task RemoveLabelSampleAsync(string labelId, string sampleId)
        {
            return RemoveAsync(sampleId, labelId);
        }
    }
}
