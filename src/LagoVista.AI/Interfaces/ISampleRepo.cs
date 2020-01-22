using LagoVista.AI.Models.TrainingData;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface ISampleRepo
    {
        Task AddSampleAsync(Sample sample);
        Task DeleteSampleDetailsAsync(string sampleId);
        Task<Sample> GetSampleAsync(string sampleId);
    }
}
