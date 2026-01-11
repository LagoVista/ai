// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 0ffe919976c1478a5b33a13f15bc7d63074ecc46fd7908ada38ed1ef7043c859
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.TrainingData;
using LagoVista.Core.Models.UIMetaData;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Repos
{
    public interface ISampleLabelRepo
    {
        Task AddSampleLabelAsync(SampleLabel label);
        Task<ListResponse<SampleLabel>> GetSamplesForLabelAsync(string labelId, string contentType, ListRequest request);
        Task RemoveSampleLabelAsync(string labelId, string sampleId);
    }
}
