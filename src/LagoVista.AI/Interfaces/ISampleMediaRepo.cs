using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface ISampleMediaRepo
    {
        Task<InvokeResult> AddSampleAsync(string orgId, string sampleId, byte[] sample);
        Task<InvokeResult> UpdateSampleAsync(string orgId, string fileName, byte[] sample);
        Task<InvokeResult<byte[]>> GetSampleAsync(string orgId, string sampleId);
    }
}
