// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: e20d68734da219495e3be88173a126a192a792cf70b002751287f1a02be23743
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.AI.Models.TrainingData;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Managers
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
