using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface ISampleMediaRepo
    {
        Task AddSampleAsync(string orgId, string sampleId, byte[] sample);
        Task UpdateSampleAsync(string orgId, string fileName, byte[] sample);
        Task<byte[]> GetSampleAsync(string orgId, string sampleId);
    }
}
