using LagoVista.AI.Models.TrainingData;
using LagoVista.Core.Models.UIMetaData;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface ISampleLabelRepo
    {
        Task AddSampleLabelAsync(SampleLabel label);
        Task<ListResponse<SampleLabel>> GetSamplesForLabelAsync(string labelId, ListRequest request);
        Task RemoveSampleLabelAsync(string labelId, string sampleId);
    }
}
