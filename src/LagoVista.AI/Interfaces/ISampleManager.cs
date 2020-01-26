using LagoVista.AI.Models;
using LagoVista.AI.Models.TrainingData;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface ISampleManager
    {
        Task<InvokeResult<Sample>> AddSampleAsync(byte[] sampleBytes, string fileName, string contentType, List<string> tagIds, EntityHeader org, EntityHeader user);
        Task<InvokeResult> UpdateSampleAsync(string sampleId, byte[] sampleBytes, EntityHeader org, EntityHeader user);
        Task<InvokeResult<byte[]>> GetSampleAsync(string sampleIdd, EntityHeader org, EntityHeader user);
        Task<SampleDetail> GetSampleDetailAsync(string sampleId, EntityHeader org, EntityHeader user);
        Task<InvokeResult> AddLabelForSampleAsync(string sampleId, string labelId, EntityHeader org, EntityHeader user);
        Task<InvokeResult> RemoveLabelFromSampleAsync(string sampleId, string labelId, EntityHeader org, EntityHeader user);
        Task<ListResponse<SampleSummary>> GetSamplesForLabelAsync(string labelId, string contentType, EntityHeader org, EntityHeader user, ListRequest request);
    }
}
