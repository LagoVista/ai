using LagoVista.AI.Models.TrainingData;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface ILabelSampleRepo
    {
        Task AddLabelSampleAsync(LabelSample label);
        Task<List<LabelSample>> GetLabelsForSampleAsync(string sampleId);
        Task RemoveLabelSampleAsync(string labelId, string sampleId);
    }
}
