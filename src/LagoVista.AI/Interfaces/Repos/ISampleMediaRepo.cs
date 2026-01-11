// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 02b52e6349fd4a52f7c48eb0298701c18df08d7ced80a355e8cb8a95d7f0e49d
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Repos
{
    public interface ISampleMediaRepo
    {
        Task<InvokeResult> AddSampleAsync(string orgId, string sampleId, byte[] sample);
        Task<InvokeResult> UpdateSampleAsync(string orgId, string fileName, byte[] sample);
        Task<InvokeResult<byte[]>> GetSampleAsync(string orgId, string sampleId);
    }
}
