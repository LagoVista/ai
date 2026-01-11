// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: d28bc7159cb626cd0fb1a9d1c55ae849584aa2fa3fa9faad756a7bcca33c4ab0
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.TrainingData;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Repos
{
    public interface ISampleRepo
    {
        Task AddSampleAsync(Sample sample);
        Task DeleteSampleDetailsAsync(string sampleId, string orgId);
        Task<Sample> GetSampleAsync(string sampleId, string orgId);
    }
}
